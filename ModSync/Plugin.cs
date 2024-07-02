using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aki.Common.Utils;
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
    [BepInPlugin("corter.modsync", "Corter ModSync", "0.5.2")]
    public class Plugin : BaseUnityPlugin
    {
        // Configuration
        Dictionary<string, ConfigEntry<bool>> configSyncPathToggles;
        ConfigEntry<bool> configDeleteRemovedFiles;

        private Persist persist;
        private string[] syncPaths = [];
        private Dictionary<string, ModFile> remoteModFiles = [];
        private List<string> addedFiles = [];
        private List<string> updatedFiles = [];
        private List<string> removedFiles = [];

        private int UpdateCount => addedFiles.Count + updatedFiles.Count + removedFiles.Count;
        private bool pluginFinished = false;
        private bool cancelledUpdate = false;
        private int downloadCount = 0;
        private string downloadDir = string.Empty;

        private readonly Server server = new();

        public static new ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("ModSync");

        private List<string> EnabledSyncPaths => syncPaths.Where((syncPath) => configSyncPathToggles[syncPath].Value).ToList();

        private void AnalyzeModFiles(Dictionary<string, ModFile> localModFiles)
        {
            remoteModFiles = server.GetRemoteModFileHashes();
            Sync.CompareModFiles(localModFiles, remoteModFiles, persist.previousSync, out addedFiles, out updatedFiles, out removedFiles);

            Logger.LogInfo($"Found {UpdateCount} files to download.");
            Logger.LogInfo($"- {addedFiles.Count} added");
            Logger.LogInfo($"- {updatedFiles.Count} updated");
            if (configDeleteRemovedFiles.Value)
                Logger.LogInfo($"- {removedFiles.Count} removed");
            else
                removedFiles.Clear();

            if (UpdateCount > 0)
                alertWindow.Show();
        }

        private void SkipUpdatingMods()
        {
            pluginFinished = true;
            alertWindow.Hide();
        }

        private async Task SyncMods()
        {
            alertWindow.Hide();
            downloadDir = Utility.GetTemporaryDirectory();

            downloadCount = 0;
            progressWindow.Show();

            var limiter = new SemaphoreSlim(32, maxCount: 32);

            var taskList = addedFiles.Union(updatedFiles).Select((file) => server.DownloadFile(file, downloadDir, limiter)).ToList();

            while (taskList.Count > 0)
            {
                var task = await Task.WhenAny(taskList);
                taskList.Remove(task);
                downloadCount++;
            }

            progressWindow.Hide();
            if (!cancelledUpdate)
                restartWindow.Show();
        }

        private void CancelUpdatingMods()
        {
            progressWindow.Hide();
            cancelledUpdate = true;

            Directory.Delete(downloadDir, true);
            downloadDir = string.Empty;

            pluginFinished = true;
        }

        private void FinishUpdatingMods()
        {
            Persist persist =
                new()
                {
                    previousSync = remoteModFiles,
                    downloadDir = downloadDir,
                    filesToDelete = configDeleteRemovedFiles.Value ? removedFiles : []
                };

            VFS.WriteTextFile(Path.Combine(Directory.GetCurrentDirectory(), ".modsync"), Json.Serialize(persist));

            Application.Quit();
        }

        private void StartPlugin()
        {
            if (persist.downloadDir != string.Empty || persist.filesToDelete.Count != 0)
            {
                Chainloader.DependencyErrors.Add(
                    $"Could not load {Info.Metadata.Name} due to failed previous update. Please ensure the PrePatcher is properly installed to 'BepInEx/patchers/Corter-ModSync-Patcher.dll' and try again."
                );
                return;
            }

            try
            {
                var version = server.GetModSyncVersion();
                Logger.LogInfo($"ModSync found server version: {version}");
                if (version != Info.Metadata.Version.ToString())
                    Logger.LogWarning(
                        "ModSync server version does not match plugin version. Found server version: " + version + ". Plugin may not work as expected!"
                    );
            }
            catch (Exception e)
            {
                Logger.LogError(e);
                Chainloader.DependencyErrors.Add(
                    $"Could not load {Info.Metadata.Name} due to request error. Please ensure the server mod is properly installed and try again."
                );
                return;
            }

            syncPaths = server.GetModSyncPaths();

            foreach (var syncPath in syncPaths)
            {
                if (Path.IsPathRooted(syncPath))
                {
                    Chainloader.DependencyErrors.Add(
                        $"Could not load {Info.Metadata.Name} due to invalid sync path. Paths must be relative to SPT server root! Invalid path '{syncPath}'"
                    );
                    return;
                }

                if (!Path.GetFullPath(syncPath).StartsWith(Directory.GetCurrentDirectory()))
                {
                    Chainloader.DependencyErrors.Add(
                        $"Could not load {Info.Metadata.Name} due to invalid sync path. Paths must be within SPT server root! Invalid path '{syncPath}'"
                    );
                    return;
                }
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

            var localModFiles = Sync.HashLocalFiles(Directory.GetCurrentDirectory(), EnabledSyncPaths);

            try
            {
                AnalyzeModFiles(localModFiles);
            }
            catch (Exception e)
            {
                Logger.LogError(e);
                Chainloader.DependencyErrors.Add(
                    $"Could not load {Info.Metadata.Name} due to error hashing local mods. Please ensure none of the files are open and try again."
                );
            }
        }

        private readonly AlertWindow alertWindow = new("Installed mods do not match server", "Would you like to update?");
        private readonly ProgressWindow progressWindow = new("Downloading Updates...", "Your game will need to be restarted\nafter update completes.");
        private readonly RestartWindow restartWindow = new("Update Complete.", "Please restart your game to continue.");

        private void Awake()
        {
            ConsoleScreen.Processor.RegisterCommand(
                "modsync",
                () =>
                {
                    StartPlugin();
                    ConsoleScreen.Log($"Found {UpdateCount} updates available.");
                }
            );

            configDeleteRemovedFiles = Config.Bind(
                "General",
                "Delete Removed Files",
                false,
                "Should the mod delete files that have been removed from the server?"
            );

            var persistPath = Path.Combine(Directory.GetCurrentDirectory(), ".modsync");
            try
            {
                persist = VFS.Exists(persistPath) ? Json.Deserialize<Persist>(File.ReadAllText(persistPath)) : new();
                persist.previousSync = persist.previousSync.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                Logger.LogError("Error parsing .modsync file.");
                Logger.LogError(e);

                Chainloader.DependencyErrors.Add(
                    $"Could not load {Info.Metadata.Name} due to error loading .modsync file. If you haven't manually modified the file, please delete it to force a fresh sync and try again."
                );
            }

            StartPlugin();
        }

        private void OnGUI()
        {
            if (Singleton<CommonUI>.Instantiated)
            {
                restartWindow.Draw(FinishUpdatingMods);
                progressWindow.Draw(downloadCount, UpdateCount, CancelUpdatingMods);
                alertWindow.Draw(() => Task.Run(() => SyncMods()), SkipUpdatingMods);
            }
        }

        public void Update()
        {
            if (alertWindow.Active || progressWindow.Active || restartWindow.Active)
            {
                if (Singleton<LoginUI>.Instantiated && Singleton<LoginUI>.Instance.gameObject.activeSelf)
                    Singleton<LoginUI>.Instance.gameObject.SetActive(false);

                if (Singleton<PreloaderUI>.Instantiated && Singleton<PreloaderUI>.Instance.gameObject.activeSelf)
                    Singleton<PreloaderUI>.Instance.gameObject.SetActive(false);

                if (Singleton<CommonUI>.Instantiated && Singleton<CommonUI>.Instance.gameObject.activeSelf)
                    Singleton<CommonUI>.Instance.gameObject.SetActive(false);
            }

            if (pluginFinished)
            {
                pluginFinished = false;
                if (Singleton<LoginUI>.Instantiated && !Singleton<LoginUI>.Instance.gameObject.activeSelf)
                    Singleton<LoginUI>.Instance.gameObject.SetActive(true);

                if (Singleton<PreloaderUI>.Instantiated && !Singleton<PreloaderUI>.Instance.gameObject.activeSelf)
                    Singleton<PreloaderUI>.Instance.gameObject.SetActive(true);

                if (Singleton<CommonUI>.Instantiated && !Singleton<CommonUI>.Instance.gameObject.activeSelf)
                    Singleton<CommonUI>.Instance.gameObject.SetActive(true);
            }
        }
    }
}
