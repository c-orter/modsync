using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    using SyncPathFileList = Dictionary<string, List<string>>;
    using SyncPathModFiles = Dictionary<string, Dictionary<string, ModFile>>;

    [BepInPlugin("corter.modsync", "Corter ModSync", "0.8.0")]
    public class Plugin : BaseUnityPlugin
    {
        private static readonly string MODSYNC_DIR = Path.Combine(Directory.GetCurrentDirectory(), "ModSync_Data");
        private static readonly string PENDING_UPDATES_DIR = Path.Combine(MODSYNC_DIR, "PendingUpdates");
        private static readonly string PREVIOUS_SYNC_PATH = Path.Combine(MODSYNC_DIR, "PreviousSync.json");
        private static readonly string REMOVED_FILES_PATH = Path.Combine(MODSYNC_DIR, "RemovedFiles.json");
        private static readonly string UPDATER_PATH = Path.Combine(Directory.GetCurrentDirectory(), "ModSync.Updater.exe");

        // Configuration
        private Dictionary<string, ConfigEntry<bool>> configSyncPathToggles;
        private ConfigEntry<bool> configDeleteRemovedFiles;

        private SyncPath[] syncPaths = [];
        private SyncPathModFiles remoteModFiles = [];
        private SyncPathModFiles previousSync = [];

        private SyncPathFileList addedFiles = [];
        private SyncPathFileList updatedFiles = [];
        private SyncPathFileList removedFiles = [];

        private List<Task> downloadTasks = [];

        private bool pluginFinished;
        private int downloadCount;
        private int totalDownloadCount;

        private readonly Server server = new();
        private CancellationTokenSource cts = new();

        public static new readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("ModSync");

        private int UpdateCount =>
            EnabledSyncPaths
                .Select(
                    (syncPath) =>
                        addedFiles[syncPath.path].Count
                        + updatedFiles[syncPath.path].Count
                        + (configDeleteRemovedFiles.Value ? removedFiles[syncPath.path].Count : 0)
                )
                .Sum();
        private bool IsDedicated => Chainloader.PluginInfos.ContainsKey("com.fika.dedicated");
        private List<SyncPath> EnabledSyncPaths => syncPaths.Where((syncPath) => configSyncPathToggles[syncPath.path].Value).ToList();

        private SyncPathFileList DownloadFiles =>
            EnabledSyncPaths
                .Select(
                    (syncPath) => new KeyValuePair<string, List<string>>(syncPath.path, addedFiles[syncPath.path].Union(updatedFiles[syncPath.path]).ToList())
                )
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        private bool SilentMode =>
            IsDedicated
            || EnabledSyncPaths.All(
                (syncPath) =>
                    syncPath.silent
                    || (
                        addedFiles[syncPath.path].Count == 0
                        && updatedFiles[syncPath.path].Count == 0
                        && (!configDeleteRemovedFiles.Value || removedFiles[syncPath.path].Count == 0)
                    )
            );

        private bool NoRestartMode =>
            EnabledSyncPaths.All(
                (syncPath) =>
                    !syncPath.restartRequired
                    || (
                        addedFiles[syncPath.path].Count == 0
                        && updatedFiles[syncPath.path].Count == 0
                        && (!configDeleteRemovedFiles.Value || removedFiles[syncPath.path].Count == 0)
                    )
            );

        private void AnalyzeModFiles(SyncPathModFiles localModFiles)
        {
            remoteModFiles = server.GetRemoteModFileHashes().ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

            Sync.CompareModFiles(EnabledSyncPaths, localModFiles, remoteModFiles, previousSync, out addedFiles, out updatedFiles, out removedFiles);

            Logger.LogInfo($"Found {UpdateCount} files to download.");
            Logger.LogInfo($"- {addedFiles.SelectMany(path => path.Value).Count()} added");
            Logger.LogInfo($"- {updatedFiles.SelectMany(path => path.Value).Count()} updated");
            if (configDeleteRemovedFiles.Value)
                Logger.LogInfo($"- {removedFiles.SelectMany(path => path.Value).Count()} removed");
            else
                removedFiles.Clear();

            if (UpdateCount > 0)
            {
                if (SilentMode)
                    Task.Run(() => SyncMods(DownloadFiles));
                else
                    updateWindow.Show();
            }
            else
                WriteModSyncData();
        }

        private void SkipUpdatingMods()
        {
            var enforcedDownloads = EnabledSyncPaths
                .Where(syncPath => syncPath.enforced)
                .Select(syncPath => new KeyValuePair<string, List<string>>(
                    syncPath.path,
                    addedFiles[syncPath.path].Union(updatedFiles[syncPath.path]).ToList()
                ))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

            if (enforcedDownloads.Values.Any(files => files.Any()))
            {
                Task.Run(() => SyncMods(enforcedDownloads));
            }
            else
            {
                pluginFinished = true;
                updateWindow.Hide();
            }
        }

        private async Task SyncMods(SyncPathFileList filesToDownload)
        {
            updateWindow.Hide();

            if (!Directory.Exists(PENDING_UPDATES_DIR))
                Directory.CreateDirectory(PENDING_UPDATES_DIR);

            downloadCount = 0;
            totalDownloadCount = 0;

            var limiter = new SemaphoreSlim(8, maxCount: 8);

            Logger.LogInfo($"Starting download of {filesToDownload.Count} files.");
            downloadTasks = EnabledSyncPaths
                .SelectMany(
                    (syncPath) =>
                        filesToDownload.TryGetValue(syncPath.path, out var pathFilesToDownload)
                            ? pathFilesToDownload.Select(
                                (file) =>
                                    server.DownloadFile(
                                        file,
                                        syncPath.restartRequired ? PENDING_UPDATES_DIR : Directory.GetCurrentDirectory(),
                                        limiter,
                                        cts.Token
                                    )
                            )
                            : []
                )
                .ToList();

            totalDownloadCount = downloadTasks.Count;

            if (!IsDedicated)
                progressWindow.Show();

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
                    progressWindow.Hide();
                    if (!IsDedicated)
                        downloadErrorWindow.Show();
                }

                downloadTasks.Remove(task);
                downloadCount++;
            }

            downloadTasks.Clear();
            progressWindow.Hide();

            Logger.LogInfo("Download of files finished.");

            if (!cts.IsCancellationRequested)
            {
                WriteModSyncData();

                if (NoRestartMode)
                {
                    Directory.Delete(PENDING_UPDATES_DIR, true);
                    pluginFinished = true;
                }
                else if (!IsDedicated)
                    restartWindow.Show();
                else
                    StartUpdaterProcess();
            }
        }

        private async Task CancelUpdatingMods()
        {
            progressWindow.Hide();
            cts.Cancel();

            await Task.WhenAll(downloadTasks);

            Directory.Delete(PENDING_UPDATES_DIR, true);
            pluginFinished = true;
        }

        private void WriteModSyncData()
        {
            VFS.WriteTextFile(PREVIOUS_SYNC_PATH, Json.Serialize(remoteModFiles));
            if (configDeleteRemovedFiles.Value && EnabledSyncPaths.Any((syncPath) => removedFiles[syncPath.path].Any()))
                VFS.WriteTextFile(REMOVED_FILES_PATH, Json.Serialize(removedFiles.SelectMany(kvp => kvp.Value).ToList()));
        }

        private void StartUpdaterProcess()
        {
            List<string> options = [];

            if (IsDedicated)
                options.Add("--silent");

            Logger.LogInfo($"Starting Updater with arguments {string.Join(" ", options)} {Process.GetCurrentProcess().Id}");
            var updaterStartInfo = new ProcessStartInfo()
            {
                FileName = UPDATER_PATH,
                Arguments = string.Join(" ", options) + " " + Process.GetCurrentProcess().Id,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var updaterProcess = new Process() { StartInfo = updaterStartInfo };

            updaterProcess.Start();
            Application.Quit();
        }

        private void StartPlugin()
        {
            cts = new();
            if (Directory.Exists(PENDING_UPDATES_DIR) || File.Exists(REMOVED_FILES_PATH))
                Logger.LogWarning(
                    "ModSync found previous update. Updater may have failed, check the 'ModSync_Data/Updater.log' for details. Attempting to continue."
                );

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
                if (Path.IsPathRooted(syncPath.path))
                {
                    Chainloader.DependencyErrors.Add(
                        $"Could not load {Info.Metadata.Name} due to invalid sync path. Paths must be relative to SPT server root! Invalid path '{syncPath}'"
                    );
                    return;
                }

                if (!Path.GetFullPath(syncPath.path).StartsWith(Directory.GetCurrentDirectory()))
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
                            syncPath.path,
                            Config.Bind(
                                "Synced Paths",
                                syncPath.path.Replace("\\", "/"),
                                syncPath.enabled,
                                $"Should the mod attempt to sync files from {syncPath}"
                            )
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
                // ReSharper disable once AsyncVoidLambda
                async () =>
                {
                    ConsoleScreen.Log($"Checking for updates.");
                    await Task.Run(StartPlugin);
                    ConsoleScreen.Log($"Found {UpdateCount} available updates.");
                }
            );

            configDeleteRemovedFiles = Config.Bind(
                "General",
                "Delete Removed Files",
                true,
                "Should the mod delete files that have been removed from the server?"
            );

            previousSync = VFS.Exists(PREVIOUS_SYNC_PATH) ? Json.Deserialize<SyncPathModFiles>(VFS.ReadTextFile(PREVIOUS_SYNC_PATH)) : [];

            if (previousSync == null)
            {
                Chainloader.DependencyErrors.Add(
                    $"Could not load {Info.Metadata.Name} due to malformed previous sync data. Please check ModSync_Data/PreviousSync.json for errors or delete it, and try again."
                );
                return;
            }

            StartPlugin();
        }

        private List<string> _optional;
        private List<string> optional =>
            _optional ??= EnabledSyncPaths
                .Where((syncPath) => !syncPath.enforced)
                .SelectMany(
                    (syncPath) =>
                        addedFiles[syncPath.path]
                            .Select((file) => $"ADDED {file}")
                            .Union(updatedFiles[syncPath.path].Select((file) => $"UPDATED {file}"))
                            .Union(configDeleteRemovedFiles.Value ? removedFiles[syncPath.path].Select((file) => $"REMOVED {file}") : [])
                )
                .ToList();

        private List<string> _required;
        private List<string> required =>
            _required ??= EnabledSyncPaths
                .Where((syncPath) => syncPath.enforced)
                .SelectMany(
                    (syncPath) =>
                        addedFiles[syncPath.path]
                            .Select((file) => $"ADDED {file}")
                            .Union(updatedFiles[syncPath.path].Select((file) => $"UPDATED {file}"))
                            .Union(configDeleteRemovedFiles.Value ? removedFiles[syncPath.path].Select((file) => $"REMOVED {file}") : [])
                )
                .ToList();

        private void OnGUI()
        {
            if (!Singleton<CommonUI>.Instantiated)
                return;

            if (restartWindow.Active)
                restartWindow.Draw(StartUpdaterProcess);

            if (progressWindow.Active)
                progressWindow.Draw(downloadCount, totalDownloadCount, required.Any() ? null : () => Task.Run(CancelUpdatingMods));

            if (updateWindow.Active)
            {
                updateWindow.Draw(
                    (optional.Any() ? string.Join("\n", optional) : "")
                        + (optional.Any() && required.Any() ? "\n\n" : "")
                        + (required.Any() ? "[Enforced]\n" + string.Join("\n", required) : ""),
                    () => Task.Run(() => SyncMods(DownloadFiles)),
                    required.Any() && !optional.Any() ? null : SkipUpdatingMods
                );
            }

            if (downloadErrorWindow.Active)
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
