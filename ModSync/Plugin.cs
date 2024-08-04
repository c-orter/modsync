using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT.UI;
using ModSync.UI;
using SPT.Common.Utils;
using UnityEngine;

namespace ModSync
{
    [BepInPlugin("corter.modsync", "Corter ModSync", "0.7.0")]
    public class Plugin : BaseUnityPlugin
    {
        // Configuration
        private Dictionary<string, ConfigEntry<bool>> configSyncPathToggles;
        private ConfigEntry<bool> configDeleteRemovedFiles;

        private Persist persist;
        private string[] syncPaths = [];
        private Dictionary<string, ModFile> remoteModFiles = [];
        private List<string> addedFiles = [];
        private List<string> updatedFiles = [];
        private List<string> removedFiles = [];

        private List<Task> downloadTasks = [];

        private int UpdateCount => addedFiles.Count + updatedFiles.Count + removedFiles.Count;
        private bool pluginFinished;
        private int downloadCount;
        private string downloadDir = string.Empty;

        private readonly Server server = new();
        private CancellationTokenSource cts = new();

        public static new readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("ModSync");
        public static bool IsDedicatedClient = false;

        private List<string> EnabledSyncPaths => syncPaths.Where((syncPath) => configSyncPathToggles[syncPath].Value).ToList();

        private void AnalyzeModFiles(Dictionary<string, ModFile> localModFiles)
        {
            remoteModFiles = server
                .GetRemoteModFileHashes()
                .Where((kvp) => EnabledSyncPaths.Any((syncPath) => kvp.Key == syncPath || kvp.Key.StartsWith($"{syncPath}\\")))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

            Sync.CompareModFiles(localModFiles, remoteModFiles, persist.previousSync, out addedFiles, out updatedFiles, out removedFiles);

            Logger.LogInfo($"Found {UpdateCount} files to download.");
            Logger.LogInfo($"- {addedFiles.Count} added");
            Logger.LogInfo($"- {updatedFiles.Count} updated");
            if (configDeleteRemovedFiles.Value)
                Logger.LogInfo($"- {removedFiles.Count} removed");
            else
                removedFiles.Clear();

            if (UpdateCount > 0)
                if (IsDedicatedClient)
                {
                    Logger.LogInfo($"Found Dedicated Plugin, skipping GUI!");
                    Task.Run(SyncMods);
                }
                else
                    updateWindow.Show();
            else
                WriteModSyncFile();
        }

        private void SkipUpdatingMods()
        {
            pluginFinished = true;
            updateWindow.Hide();
        }

        private async Task SyncMods()
        {
            if(!IsDedicatedClient)
                updateWindow.Hide();
            downloadDir = Utility.GetTemporaryDirectory();

            downloadCount = 0;

            if(!IsDedicatedClient)
                progressWindow.Show();

            var limiter = new SemaphoreSlim(8, maxCount: 8);

            downloadTasks = addedFiles.Union(updatedFiles).Select((file) => server.DownloadFile(file, downloadDir, limiter, cts.Token)).ToList();

            while (downloadTasks.Count > 0 && !cts.IsCancellationRequested)
            {
                var task = await Task.WhenAny(downloadTasks);

                try
                {
                    await task;
                }
                catch (Exception e)
                {
                    if (e is TaskCanceledException && cts.IsCancellationRequested)
                        continue;

                    cts.Cancel();
                    if(!IsDedicatedClient)
                    {
                        progressWindow.Hide();
                        downloadErrorWindow.Show();
                    }
                    else
                    {
                        Logger.LogError(e);
                    }
                }

                downloadTasks.Remove(task);
                downloadCount++;
            }

            downloadTasks.Clear();

            if(!IsDedicatedClient)
                progressWindow.Hide();

            if (!cts.IsCancellationRequested)
            {
                WriteModSyncFile();
                if (!IsDedicatedClient)
                    restartWindow.Show();
                else
                {
                    Logger.LogInfo($"Restarting Dedicated Client for the mods to take effects!");
                    Application.Quit();
                }
            }
        }

        private async Task CancelUpdatingMods()
        {
            progressWindow.Hide();
            cts.Cancel();

            await Task.WhenAll(downloadTasks);

            Directory.Delete(downloadDir, true);
            downloadDir = string.Empty;

            pluginFinished = true;
        }

        private void WriteModSyncFile()
        {
            Persist newPersist =
                new()
                {
                    previousSync = remoteModFiles,
                    downloadDir = downloadDir,
                    filesToDelete = configDeleteRemovedFiles.Value ? removedFiles : [],
                    version = Persist.LATEST_VERSION
                };

            VFS.WriteTextFile(Path.Combine(Directory.GetCurrentDirectory(), ".modsync"), Json.Serialize(newPersist));
        }

        private void StartPlugin()
        {
            if (Chainloader.PluginInfos.ContainsKey("com.fika.dedicated"))
                IsDedicatedClient = true;

            cts = new();
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

            var localModFiles = Sync.HashLocalFiles(Directory.GetCurrentDirectory(), [.. syncPaths], EnabledSyncPaths);

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

        private readonly UpdateWindow updateWindow = new("Installed mods do not match server", "Would you like to update?");
        private readonly ProgressWindow progressWindow = new("Downloading Updates...", "Your game will need to be restarted\nafter update completes.");
        private readonly AlertWindow restartWindow = new(new(480f, 200f), "Update Complete.", "Please restart your game to continue.");
        private readonly AlertWindow downloadErrorWindow =
            new(
                new(640f, 240f),
                "Download failed!",
                "There was an error updating mod files.\nPlease check BepInEx/LogOutput.log for more information.",
                "QUIT"
            );

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
                true,
                "Should the mod delete files that have been removed from the server?"
            );

            var persistPath = Path.Combine(Directory.GetCurrentDirectory(), ".modsync");
            try
            {
                persist = VFS.Exists(persistPath) ? Json.Deserialize<Persist>(File.ReadAllText(persistPath)) : new();
                Logger.LogInfo($"Parsed .modsync file with version {persist.version}");
                if (persist.version < Persist.LATEST_VERSION)
                {
                    Logger.LogInfo(".modsync file format has been updated. Ignoring old file...");
                    persist = new Persist { version = Persist.LATEST_VERSION };
                }

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
            if (!Singleton<CommonUI>.Instantiated)
                return;

            restartWindow.Draw(Application.Quit);
            progressWindow.Draw(downloadCount, addedFiles.Count + updatedFiles.Count, () => Task.Run(CancelUpdatingMods));
            updateWindow.Draw(addedFiles, updatedFiles, configDeleteRemovedFiles.Value ? removedFiles : [], () => Task.Run(SyncMods), SkipUpdatingMods);
            downloadErrorWindow.Draw(Application.Quit);
        }

        public void Update()
        {
            if (updateWindow.Active || progressWindow.Active || restartWindow.Active || downloadErrorWindow.Active)
            {
                if (Singleton<LoginUI>.Instantiated && Singleton<LoginUI>.Instance.gameObject.activeSelf)
                    Singleton<LoginUI>.Instance.gameObject.SetActive(false);

                if (Singleton<PreloaderUI>.Instantiated && Singleton<PreloaderUI>.Instance.gameObject.activeSelf)
                    Singleton<PreloaderUI>.Instance.gameObject.SetActive(false);

                if (Singleton<CommonUI>.Instantiated && Singleton<CommonUI>.Instance.gameObject.activeSelf)
                    Singleton<CommonUI>.Instance.gameObject.SetActive(false);
            }
            else if (pluginFinished)
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
