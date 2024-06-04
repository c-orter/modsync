using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aki.Common.Http;
using Aki.Common.Utils;
using Aki.Custom.Utils;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT.UI;
using UnityEngine;

namespace ModSync
{
    internal class ModFile
    {
        public ModFile(uint crc, long modified)
        {
            this.crc = crc;
            this.modified = modified;
        }

        public uint crc;
        public long modified;
    }

    [BepInPlugin("xyz.corter.modsync", "ModSync", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        // Configuration
        private ConfigEntry<bool> configSyncServerMods;

        private const int CONFIRMATION_DURATION = 15;
        private Dictionary<string, ModFile> clientModDiff = new();
        private Dictionary<string, ModFile> serverModDiff = new();
        private bool _isPopupOpen = false;
        private bool _showProgress = false;
        private bool _cancelledUpdate = false;
        private int _downloaded = 0;
        private string _tempDir = string.Empty;

        public static new ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("ModSync");

        private async Task<Dictionary<string, ModFile>> HashLocalFiles(string baseDir, string[] subdirs)
        {
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), baseDir);

            var files = new Dictionary<string, ModFile>();

            foreach (var subdir in subdirs)
            {
                var path = Path.Combine(basePath, subdir);
                foreach (var file in Utility.GetFilesInDir(path))
                {
                    var data = await VFS.ReadFileAsync(file);
                    files.Add(file, new ModFile(Crc32.Compute(data), ((DateTimeOffset)File.GetLastWriteTimeUtc(file)).ToUnixTimeMilliseconds()));
                }
            }

            return files;
        }

        private Dictionary<string, ModFile> CompareLocalFiles(Dictionary<string, ModFile> localFiles, Dictionary<string, ModFile> remoteFiles)
        {
            var modifiedFiles = new Dictionary<string, ModFile>();

            foreach (var kvp in remoteFiles)
            {
                if (!localFiles.ContainsKey(kvp.Key))
                    modifiedFiles.Add(kvp.Key, kvp.Value);
                else if (kvp.Value.crc != localFiles[kvp.Key].crc && kvp.Value.modified > localFiles[kvp.Key].modified)
                    modifiedFiles.Add(kvp.Key, kvp.Value);
            }

            return modifiedFiles;
        }

        private async Task CheckLocalMods()
        {
            var localClientFiles = await HashLocalFiles("BepInEx", ["plugins", "config"]);
            var clientResponse = await RequestHandler.GetJsonAsync("/launcher/client/hashModFiles");
            var remoteClientFiles = Json.Deserialize<Dictionary<string, ModFile>>(clientResponse);

            clientModDiff = CompareLocalFiles(localClientFiles, remoteClientFiles);

            if (configSyncServerMods.Value)
            {
                var localServerFiles = await HashLocalFiles("user", ["mods"]);
                var serverResponse = await RequestHandler.GetJsonAsync("/launcher/server/hashModFiles");
                var remoteServerFiles = Json.Deserialize<Dictionary<string, ModFile>>(serverResponse);

                serverModDiff = CompareLocalFiles(localServerFiles, remoteServerFiles);
            }

            Logger.LogInfo($"Found {clientModDiff.Count + serverModDiff.Count} files to download.");
        }

        private void SkipUpdatingMods()
        {
            return;
        }

        private void BackupModFolders(string baseDir, string[] subDirs, string tempDir)
        {
            foreach (var subDir in subDirs)
            {
                var sourceDir = Path.Combine(baseDir, subDir);
                var targetDir = Path.Combine(tempDir, subDir);
                VFS.CreateDirectory(targetDir);
                Utility.CopyFilesRecursively(sourceDir, targetDir);
            }
        }

        private async Task DownloadMods(Dictionary<string, ModFile> modDiff, string baseUrl, string baseDir)
        {
            foreach (var file in modDiff.Keys)
            {
                if (_cancelledUpdate)
                    return;
                var data = await RequestHandler.GetDataAsync($"{baseUrl}/{file}");

                var pathParts = file.Split('/');
                for (int i = 0; i < pathParts.Length - 1; i++)
                {
                    var subDirectory = Path.Combine(baseDir, string.Join(Path.PathSeparator.ToString(), pathParts.Take(i + 1)));

                    if (!VFS.Exists(subDirectory))
                        VFS.CreateDirectory(subDirectory);
                }

                var fullPath = Path.Combine(baseDir, string.Join(Path.PathSeparator.ToString(), pathParts));
                if (_cancelledUpdate)
                    return;
                await VFS.WriteFileAsync(fullPath, data);

                _downloaded++;
            }
        }

        private async Task UpdateMods()
        {
            _tempDir = Utility.GetTemporaryDirectory();

            var clientBaseDir = Path.Combine(Directory.GetCurrentDirectory(), "BepInEx");
            var clientTempDir = Path.Combine(_tempDir, "clientMods");
            VFS.CreateDirectory(clientTempDir);
            BackupModFolders(clientBaseDir, ["plugins", "config"], clientTempDir);

            _downloaded = 0;
            _showProgress = true;
            await DownloadMods(clientModDiff, "/launcher/client/fetchModFile", clientBaseDir);

            if (configSyncServerMods.Value && !_cancelledUpdate)
            {
                var serverBaseDir = Path.Combine(Directory.GetCurrentDirectory(), "user");
                var serverTempDir = Path.Combine(_tempDir, "serverMods");
                VFS.CreateDirectory(serverTempDir);
                BackupModFolders(serverBaseDir, ["mods"], serverTempDir);
                await DownloadMods(serverModDiff, "/launcher/server/fetchModFile", Path.Combine(Directory.GetCurrentDirectory(), "user"));
            }
        }

        private void RestoreBackup(string baseDir, string[] subDirs, string tempDir)
        {
            foreach (var subDir in subDirs)
            {
                var sourceDir = Path.Combine(tempDir, subDir);
                var targetDir = Path.Combine(baseDir, subDir);
                Directory.Delete(targetDir, true);
                VFS.CreateDirectory(targetDir);
                Utility.CopyFilesRecursively(sourceDir, targetDir);
            }

            Directory.Delete(tempDir, true);
        }

        private void CancelUpdatingMods()
        {
            _cancelledUpdate = true;

            var clientBaseDir = Path.Combine(Directory.GetCurrentDirectory(), "BepInEx");
            var clientTempDir = Path.Combine(_tempDir, "clientMods");
            RestoreBackup(clientBaseDir, ["plugins", "config"], clientTempDir);

            if (configSyncServerMods.Value)
            {
                var serverBaseDir = Path.Combine(Directory.GetCurrentDirectory(), "user");
                var serverTempDir = Path.Combine(_tempDir, "serverMods");
                RestoreBackup(serverBaseDir, ["mods"], serverTempDir);
            }

            Directory.Delete(_tempDir, true);
            _tempDir = string.Empty;
        }

        private void FinishUpdatingMods()
        {
            Directory.Delete(_tempDir, true);
            _tempDir = string.Empty;

            Application.Quit();
        }

        private void Awake()
        {
            configSyncServerMods = Config.Bind("General", "SyncServerMods", false, "Sync server mods to client");

            Logger.LogInfo("ModSync plugin loaded.");

            Task.Run(() => CheckLocalMods());
        }

        public void Update()
        {
            if (Singleton<PreloaderUI>.Instantiated)
            {
                if (!_isPopupOpen && (clientModDiff.Count > 0 || (configSyncServerMods.Value && serverModDiff.Count > 0)))
                {
                    _isPopupOpen = true;
                    Singleton<PreloaderUI>.Instance.ShowMismatchedModScreen(
                        "Installed client mods do not match server.",
                        "Please wait {0} seconds to automatically update them and restart the game.",
                        "(Click below to start update.)",
                        "(Or click below to ignore update.)",
                        CONFIRMATION_DURATION,
                        () => Task.Run(() => SkipUpdatingMods()),
                        () => Task.Run(() => UpdateMods())
                    );
                }
                else if (_showProgress && (clientModDiff.Count > 0 || (configSyncServerMods.Value && serverModDiff.Count > 0)))
                {
                    _showProgress = false;
                    Singleton<PreloaderUI>.Instance.ShowProgressScreen(
                        "Downloading Client Mods",
                        "Downloading client mods...",
                        clientModDiff.Count + (configSyncServerMods.Value ? serverModDiff.Count : 0),
                        () => _downloaded,
                        () => Task.Run(() => CancelUpdatingMods()),
                        () => Task.Run(() => FinishUpdatingMods())
                    );
                }
            }
        }
    }
}
