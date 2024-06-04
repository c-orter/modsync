using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aki.Common.Http;
using Aki.Common.Utils;
using Aki.Custom.Utils;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT.UI;
using UnityEngine;

namespace ModSync
{
    internal class ModFile(uint crc, long modified, bool nosync = false)
    {
        public uint crc = crc;
        public long modified = modified;
        public bool nosync = nosync;
    }

    [BepInPlugin("xyz.corter.modsync", "ModSync", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        // Configuration
        private ConfigEntry<bool> configSyncServerMods;

        private const int CONFIRMATION_DURATION = 15;
        private Dictionary<string, ModFile> clientModDiff = [];
        private Dictionary<string, ModFile> serverModDiff = [];
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
                var subDirNoSync = VFS.Exists(VFS.Combine(path, ".nosync")) || VFS.Exists(VFS.Combine(path, ".nosync.txt"));
                foreach (var file in Utility.GetFilesInDir(path))
                {
                    var data = await VFS.ReadFileAsync(file);
                    files.Add(
                        file.Replace($"{basePath}\\", ""),
                        new ModFile(
                            Crc32.Compute(data),
                            ((DateTimeOffset)File.GetLastWriteTimeUtc(file)).ToUnixTimeMilliseconds(),
                            subDirNoSync || VFS.Exists($"{file}.nosync") || VFS.Exists($"{file}.nosync.txt")
                        )
                    );
                }
            }

            return files;
        }

        private Dictionary<string, ModFile> CompareLocalFiles(Dictionary<string, ModFile> localFiles, Dictionary<string, ModFile> remoteFiles)
        {
            var modifiedFiles = new Dictionary<string, ModFile>();

            foreach (var kvp in remoteFiles)
            {
                if (kvp.Value.nosync)
                    continue;
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
            var clientResponse = await RequestHandler.GetJsonAsync("/modsync/client/hashes");
            var remoteClientFiles = Json.Deserialize<Dictionary<string, ModFile>>(clientResponse);

            clientModDiff = CompareLocalFiles(localClientFiles, remoteClientFiles);

            if (configSyncServerMods.Value)
            {
                var localServerFiles = await HashLocalFiles("user", ["mods"]);
                var serverResponse = await RequestHandler.GetJsonAsync("/modsync/server/hashes");
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

                var fullPath = Path.Combine(baseDir, file);
                if (_cancelledUpdate)
                    return;
                await Utility.WriteFileAsync(fullPath, data);

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
            await DownloadMods(clientModDiff, "/modsync/client/fetch", clientBaseDir);

            if (configSyncServerMods.Value && !_cancelledUpdate)
            {
                var serverBaseDir = Path.Combine(Directory.GetCurrentDirectory(), "user");
                var serverTempDir = Path.Combine(_tempDir, "serverMods");
                VFS.CreateDirectory(serverTempDir);
                BackupModFolders(serverBaseDir, ["mods"], serverTempDir);
                await DownloadMods(serverModDiff, "/modsync/server/fetch", Path.Combine(Directory.GetCurrentDirectory(), "user"));
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

            Task.Run(async () =>
            {
                try
                {
                    var response = await RequestHandler.GetJsonAsync("/modsync/version");
                    await CheckLocalMods();
                }
                catch
                {
                    Chainloader.DependencyErrors.Add($"Could not load {Info.Metadata.Name} due to request error. Is the server mod installed?");
                }
            });
        }

        public void Update()
        {
            if (Singleton<PreloaderUI>.Instantiated)
            {
                if (!_isPopupOpen && (clientModDiff.Count > 0 || (configSyncServerMods.Value && serverModDiff.Count > 0)))
                {
                    _isPopupOpen = true;
                    Singleton<PreloaderUI>.Instance.ShowMismatchedModScreen(
                        "Installed mods do not match server.",
                        "Please wait {0} seconds before updating them.",
                        "(Click below to start update)",
                        "(Or click below to ignore updates)",
                        CONFIRMATION_DURATION,
                        () => Task.Run(() => SkipUpdatingMods()),
                        () => Task.Run(() => UpdateMods())
                    );
                }
                else if (_showProgress && (clientModDiff.Count > 0 || (configSyncServerMods.Value && serverModDiff.Count > 0)))
                {
                    _showProgress = false;
                    Singleton<PreloaderUI>.Instance.ShowProgressScreen(
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
