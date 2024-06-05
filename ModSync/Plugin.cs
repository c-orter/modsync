using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
using BepInPaths = BepInEx.Paths;

namespace ModSync
{
    internal class ModFile(uint crc, long modified, bool nosync = false)
    {
        public uint crc = crc;
        public long modified = modified;
        public bool nosync = nosync;
    }

    [BepInPlugin("aaa.corter.modsync", "ModSync", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        // Configuration
        private ConfigEntry<bool> configSyncServerMods;

        private const int CONFIRMATION_DURATION = 10;
        private Dictionary<string, ModFile> clientModDiff = [];
        private Dictionary<string, ModFile> serverModDiff = [];
        private bool _showMismatched = true;
        private bool _showProgress = false;
        private bool _cancelledUpdate = false;
        private int _downloaded = 0;
        private string _tempDir = string.Empty;

        public static new ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("ModSync");

        private KeyValuePair<string, ModFile> CreateModFile(string basePath, string file)
        {
            var data = VFS.ReadFile(file);
            var relativePath = file.Replace($"{basePath}\\", "");

            return new KeyValuePair<string, ModFile>(
                relativePath,
                new ModFile(
                    Crc32.Compute(data),
                    ((DateTimeOffset)File.GetLastWriteTimeUtc(file)).ToUnixTimeMilliseconds(),
                    Utility.NoSyncInTree(basePath, relativePath) || VFS.Exists($"{file}.nosync") || VFS.Exists($"{file}.nosync.txt")
                )
            );
        }

        private Dictionary<string, ModFile> HashLocalFiles(string baseDir, string[] subDirs)
        {
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), baseDir);

            return subDirs
                .Select((subDir) => Path.Combine(basePath, subDir))
                .SelectMany((path) => Utility.GetFilesInDir(path).AsParallel().Select((file) => CreateModFile(basePath, file)))
                .ToDictionary(item => item.Key, item => item.Value);
        }

        private Dictionary<string, ModFile> CompareLocalFiles(Dictionary<string, ModFile> localFiles, Dictionary<string, ModFile> remoteFiles)
        {
            return remoteFiles
                .Where(
                    (kvp) =>
                        !localFiles.ContainsKey(kvp.Key)
                        || (!localFiles[kvp.Key].nosync && kvp.Value.crc != localFiles[kvp.Key].crc && kvp.Value.modified > localFiles[kvp.Key].modified)
                )
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private async Task CheckLocalMods(Dictionary<string, ModFile> localClientFiles, Dictionary<string, ModFile> localServerFiles)
        {
            var clientResponse = await RequestHandler.GetJsonAsync("/modsync/client/hashes");
            var remoteClientFiles = Json.Deserialize<Dictionary<string, ModFile>>(clientResponse);

            clientModDiff = CompareLocalFiles(localClientFiles, remoteClientFiles);

            if (configSyncServerMods.Value)
            {
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

        private async Task DownloadMod(string file, string baseUrl, string baseDir, SemaphoreSlim limiter)
        {
            await limiter.WaitAsync();
            if (_cancelledUpdate)
                return;

            var data = await RequestHandler.GetDataAsync($"{baseUrl}/{file}");

            var fullPath = Path.Combine(baseDir, file);
            if (_cancelledUpdate)
                return;

            await Utility.WriteFileAsync(fullPath, data);
        }

        private async Task DownloadMods(Dictionary<string, ModFile> modDiff, string baseUrl, string baseDir)
        {
            var limiter = new SemaphoreSlim(32, maxCount: 32);
            var taskList = modDiff.Keys.Select((file) => DownloadMod(file, baseUrl, baseDir, limiter)).ToList();

            while (taskList.Count > 0)
            {
                var task = await Task.WhenAny(taskList);
                taskList.Remove(task);
                limiter.Release();
                _downloaded++;
            }

            limiter.Dispose();
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
            var configFile = new ConfigFile(Path.Combine(BepInPaths.ConfigPath, "corter.modsync.cfg"), true);

            configSyncServerMods = configFile.Bind("General", "SyncServerMods", false, "Sync server mods to client");

            var localClientFiles = HashLocalFiles("BepInEx", ["plugins", "config"]);
            var localServerFiles = configSyncServerMods.Value ? HashLocalFiles("user", ["mods"]) : [];

            Task.Run(async () =>
            {
                try
                {
                    var response = Json.Deserialize<Dictionary<string, string>>(await RequestHandler.GetJsonAsync("/modsync/version"));
                    Logger.LogInfo($"ModSync found server version: {response["version"]}");
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                    Chainloader.DependencyErrors.Add(
                        $"Could not load {Info.Metadata.Name} due to request error. Please ensure the server mod is properly installed and try again."
                    );
                    return;
                }

                try
                {
                    await CheckLocalMods(localClientFiles, localServerFiles);
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                    Chainloader.DependencyErrors.Add(
                        $"Could not load {Info.Metadata.Name} due to error hashing local mods. Please ensure none of the files are open and try again."
                    );
                }
            });
        }

        public void Update()
        {
            if (Singleton<PreloaderUI>.Instantiated)
            {
                if (_showMismatched && (clientModDiff.Count > 0 || (configSyncServerMods.Value && serverModDiff.Count > 0)))
                {
                    _showMismatched = false;
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
                        "Downloading mods from server...",
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
