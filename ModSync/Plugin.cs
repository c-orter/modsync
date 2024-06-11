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

    [BepInPlugin("aaa.corter.modsync", "Corter ModSync", "0.3.2")]
    public class Plugin : BaseUnityPlugin
    {
        // Configuration
        Dictionary<string, ConfigEntry<bool>> configSyncPathToggles;
        private string[] syncPaths = [];
        private Dictionary<string, ModFile> fileHashDiff = [];
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

        private async Task CheckLocalMods(Dictionary<string, ModFile> localModFiles)
        {
            var clientResponse = await RequestHandler.GetJsonAsync("/modsync/hashes");
            var remoteModFiles = Json.Deserialize<Dictionary<string, ModFile>>(clientResponse);

            fileHashDiff = CompareLocalFiles(localModFiles, remoteModFiles);

            Logger.LogInfo($"Found {fileHashDiff.Count} files to download.");
            mismatchedMods = fileHashDiff.Count > 0;
        }

        private void SkipUpdatingMods()
        {
            showMenu = true;
            mismatchedMods = false;
        }

        private void BackupModPath(string syncPath, string tempDir)
        {
            var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), syncPath);
            var targetPath = Path.Combine(tempDir, syncPath);
            Logger.LogWarning($"Backing up {sourcePath} to {targetPath}");
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, targetPath);
            }
            else if (Directory.Exists(sourcePath))
            {
                VFS.CreateDirectory(targetPath);
                Utility.CopyFilesRecursively(sourcePath, targetPath);
            }
        }

        private async Task DownloadMod(string file, string baseUrl, SemaphoreSlim limiter)
        {
            await limiter.WaitAsync();
            if (cancelledUpdate)
                return;

            var data = await RequestHandler.GetDataAsync($"{baseUrl}/{file}");

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), file);
            Logger.LogWarning($"Downloading {file} to {fullPath}");
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
            Logger.LogWarning($"Running backup to {backupDir}");

            foreach (var syncPath in syncPaths.Where((syncPath) => configSyncPathToggles[syncPath].Value))
                BackupModPath(syncPath, backupDir);

            Logger.LogWarning("Finished backup");

            downloadCount = 0;
            downloadingMods = true;
            await DownloadMods(fileHashDiff, "/modsync/fetch");

            if (!cancelledUpdate)
                restartRequired = true;
        }

        private void RestoreBackup(string syncPath, string tempDir)
        {
            var sourcePath = Path.Combine(tempDir, syncPath);
            var targetPath = Path.Combine(Directory.GetCurrentDirectory(), syncPath);
            if (File.Exists(sourcePath))
            {
                File.Delete(targetPath);
                File.Copy(sourcePath, targetPath);
            }
            else
            {
                Directory.Delete(targetPath, true);
                VFS.CreateDirectory(targetPath);
                Utility.CopyFilesRecursively(sourcePath, targetPath);
            }
        }

        private void CancelUpdatingMods()
        {
            downloadingMods = false;
            cancelledUpdate = true;

            foreach (var subDir in syncPaths.Where((subDir) => configSyncPathToggles[subDir].Value))
                RestoreBackup(subDir, backupDir);

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
            syncPaths = Json.Deserialize<string[]>(RequestHandler.GetJson("/modsync/paths"));

            if (syncPaths.Any((dir) => Path.IsPathRooted(dir) || !Path.GetFullPath(dir).StartsWith(Directory.GetCurrentDirectory())))
            {
                Chainloader.DependencyErrors.Add(
                    $"Could not load {Info.Metadata.Name} due to invalid client mod directory. Please ensure server configuration is not trying to validate files outside of the SPT directory"
                );
                return;
            }

            configSyncPathToggles = syncPaths
                .Select(
                    (syncPath) =>
                        new KeyValuePair<string, ConfigEntry<bool>>(
                            syncPath,
                            Config.Bind("Synced Paths", syncPath.Replace("\\", "/"), true, $"Should the mod attempt to sync files from {syncPath}")
                        )
                )
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var localModFiles = HashLocalFiles(
                syncPaths
                    .Where((syncPath) => configSyncPathToggles[syncPath].Value && VFS.Exists(Path.Combine(Directory.GetCurrentDirectory(), syncPath)))
                    .ToArray()
            );

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
                    await CheckLocalMods(localModFiles);
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
            progressWindow = new ProgressWindow("Downloading Updates...", "Your game will need to be restarted\nafter updates complete.");
            restartWindow = new RestartWindow("Update Complete.", "Please restart your game to continue.");
        }

        private void OnGUI()
        {
            if (Singleton<CommonUI>.Instantiated)
            {
                if (restartRequired)
                    restartWindow.Draw(FinishUpdatingMods);
                else if (downloadingMods)
                    progressWindow.Draw(downloadCount, fileHashDiff.Count, CancelUpdatingMods);
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
