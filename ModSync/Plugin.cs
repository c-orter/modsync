using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using ModSync.UI;
using UnityEngine;

namespace ModSync
{
    internal class ModFile(uint crc, long modified, bool nosync = false)
    {
        public uint crc = crc;
        public long modified = modified;
        public bool nosync = nosync;
    }

    [BepInPlugin("aaa.corter.modsync", "Corter ModSync", "0.3.0")]
    public class Plugin : BaseUnityPlugin
    {
        // Configuration
        private ConfigEntry<bool> configSyncServerMods;

        private string[] clientDirs = [];
        private string[] serverDirs = [];

        private Dictionary<string, ModFile> clientModDiff = [];
        private Dictionary<string, ModFile> serverModDiff = [];
        private bool mismatchedMods = false;
        private bool showMenu = false;
        private bool downloadingMods = false;
        private bool restartRequired = false;
        private bool cancelledUpdate = false;
        private int downloadCount = 0;
        private string backupDir = string.Empty;

        public static new ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("ModSync");

        private KeyValuePair<string, ModFile> CreateModFile(string file)
        {
            var data = VFS.ReadFile(file);
            var relativePath = file.Replace($"{Directory.GetCurrentDirectory()}\\", "");

            return new KeyValuePair<string, ModFile>(
                relativePath,
                new ModFile(
                    Crc32.Compute(data),
                    ((DateTimeOffset)File.GetLastWriteTimeUtc(file)).ToUnixTimeMilliseconds(),
                    Utility.NoSyncInTree(Directory.GetCurrentDirectory(), relativePath) || VFS.Exists($"{file}.nosync") || VFS.Exists($"{file}.nosync.txt")
                )
            );
        }

        private Dictionary<string, ModFile> HashLocalFiles(string[] dirs)
        {
            return dirs.Select((subDir) => Path.Combine(Directory.GetCurrentDirectory(), subDir))
                .SelectMany((path) => Utility.GetFilesInDir(path).AsParallel().Select((file) => CreateModFile(file)))
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
            mismatchedMods = clientModDiff.Count + serverModDiff.Count > 0;
        }

        private void SkipUpdatingMods()
        {
            showMenu = true;
            mismatchedMods = false;
        }

        private void BackupModFolders(string[] dirs, string tempDir)
        {
            foreach (var subDir in dirs)
            {
                var sourceDir = Path.Combine(Directory.GetCurrentDirectory(), subDir);
                var targetDir = Path.Combine(tempDir, subDir);
                VFS.CreateDirectory(targetDir);
                Utility.CopyFilesRecursively(sourceDir, targetDir);
            }
        }

        private async Task DownloadMod(string file, string baseUrl, SemaphoreSlim limiter)
        {
            await limiter.WaitAsync();
            if (cancelledUpdate)
                return;

            var data = await RequestHandler.GetDataAsync($"{baseUrl}/{file}");

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), file);
            if (cancelledUpdate)
                return;

            await Utility.WriteFileAsync(fullPath, data);
        }

        private async Task DownloadMods(Dictionary<string, ModFile> modDiff, string baseUrl)
        {
            var limiter = new SemaphoreSlim(32, maxCount: 32);
            var taskList = modDiff.Keys.Select((file) => DownloadMod(file, baseUrl, limiter)).ToList();

            while (taskList.Count > 0)
            {
                var task = await Task.WhenAny(taskList);
                taskList.Remove(task);
                limiter.Release();
                downloadCount++;
            }

            limiter.Dispose();
        }

        private async Task UpdateMods()
        {
            mismatchedMods = false;
            backupDir = Utility.GetTemporaryDirectory();

            var clientTempDir = Path.Combine(backupDir, "clientMods");
            VFS.CreateDirectory(clientTempDir);
            BackupModFolders(clientDirs, clientTempDir);

            downloadCount = 0;
            downloadingMods = true;
            await DownloadMods(clientModDiff, "/modsync/client/fetch");

            if (configSyncServerMods.Value && !cancelledUpdate)
            {
                var serverTempDir = Path.Combine(backupDir, "serverMods");
                VFS.CreateDirectory(serverTempDir);
                BackupModFolders(serverDirs, serverTempDir);
                await DownloadMods(serverModDiff, "/modsync/server/fetch");
            }

            if (!cancelledUpdate)
                restartRequired = true;
        }

        private void RestoreBackup(string[] dirs, string tempDir)
        {
            foreach (var subDir in dirs)
            {
                var sourceDir = Path.Combine(tempDir, subDir);
                var targetDir = Path.Combine(Directory.GetCurrentDirectory(), subDir);
                Directory.Delete(targetDir, true);
                VFS.CreateDirectory(targetDir);
                Utility.CopyFilesRecursively(sourceDir, targetDir);
            }

            Directory.Delete(tempDir, true);
        }

        private void CancelUpdatingMods()
        {
            downloadingMods = false;
            cancelledUpdate = true;

            var clientTempDir = Path.Combine(backupDir, "clientMods");
            RestoreBackup(clientDirs, clientTempDir);

            if (configSyncServerMods.Value)
            {
                var serverTempDir = Path.Combine(backupDir, "serverMods");
                RestoreBackup(serverDirs, serverTempDir);
            }

            Directory.Delete(backupDir, true);
            backupDir = string.Empty;

            showMenu = true;
        }

        private void FinishUpdatingMods()
        {
            Directory.Delete(backupDir, true);
            backupDir = string.Empty;

            Application.Quit();
        }

        private void Awake()
        {
            configSyncServerMods = Config.Bind("General", "SyncServerMods", false, "Sync server mods to client");

            clientDirs = Json.Deserialize<string[]>(RequestHandler.GetJson("/modsync/client/dirs"));
            var localClientFiles = HashLocalFiles(clientDirs);

            Dictionary<string, ModFile> localServerFiles = [];
            if (configSyncServerMods.Value)
            {
                serverDirs = Json.Deserialize<string[]>(RequestHandler.GetJson("/modsync/server/dirs"));
                localServerFiles = HashLocalFiles(serverDirs);
            }

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

        private AlertWindow alertWindow;
        private ProgressWindow progressWindow;
        private RestartWindow restartWindow;

        private void Start()
        {
            alertWindow = new AlertWindow("Installed mods do not match server", "Would you like to update?");
            progressWindow = new ProgressWindow("Downloading Updates...", "Your game will need to be restarted\nafter update completes.");
            restartWindow = new RestartWindow("Update Complete.", "Please restart your game to continue.");
        }

        private void OnGUI()
        {
            if (Singleton<CommonUI>.Instantiated)
            {
                if (restartRequired)
                    restartWindow.Draw(FinishUpdatingMods);
                else if (downloadingMods)
                    progressWindow.Draw(downloadCount, clientModDiff.Count + serverModDiff.Count, CancelUpdatingMods);
                else if (mismatchedMods)
                    alertWindow.Draw(() => Task.Run(() => UpdateMods()), SkipUpdatingMods);
            }
        }

        public void Update()
        {
            if (mismatchedMods || downloadingMods || restartRequired)
            {
                if (Singleton<LoginUI>.Instantiated && Singleton<LoginUI>.Instance.gameObject.activeSelf)
                    Singleton<LoginUI>.Instance.gameObject.SetActive(false);
                if (Singleton<PreloaderUI>.Instantiated && Singleton<PreloaderUI>.Instance.gameObject.activeSelf)
                    Singleton<PreloaderUI>.Instance.gameObject.SetActive(false);

                if (Singleton<CommonUI>.Instantiated && Singleton<CommonUI>.Instance.gameObject.activeSelf)
                    Singleton<CommonUI>.Instance.gameObject.SetActive(false);
            }

            if (showMenu)
            {
                showMenu = false;

                if (Singleton<PreloaderUI>.Instantiated && !Singleton<PreloaderUI>.Instance.gameObject.activeSelf)
                    Singleton<PreloaderUI>.Instance.gameObject.SetActive(true);

                if (Singleton<CommonUI>.Instantiated && !Singleton<CommonUI>.Instance.gameObject.activeSelf)
                    Singleton<CommonUI>.Instance.gameObject.SetActive(true);
            }
        }
    }
}
