using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SPT.Common.Utils;
using SPT.Custom.Utils;

namespace ModSync
{
    public static class Sync
    {
        public static Dictionary<string, List<string>> GetAddedFiles(
            List<SyncPath> syncPaths,
            Dictionary<string, Dictionary<string, ModFile>> localModFiles,
            Dictionary<string, Dictionary<string, ModFile>> remoteModFiles
        )
        {
            Plugin.Logger.LogInfo($"Local Mod Files: {Json.Serialize(localModFiles)}");
            Plugin.Logger.LogInfo($"Remote Mod Files: {Json.Serialize(remoteModFiles)}");

            return syncPaths
                .Select(
                    (syncPath) =>
                        new KeyValuePair<string, List<string>>(
                            syncPath.path,
                            remoteModFiles[syncPath.path]
                                .Keys.Where((file) => !remoteModFiles[syncPath.path][file].nosync)
                                .Except(localModFiles[syncPath.path].Keys, StringComparer.OrdinalIgnoreCase)
                                .ToList()
                        )
                )
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public static Dictionary<string, List<string>> GetUpdatedFiles(
            List<SyncPath> syncPaths,
            Dictionary<string, Dictionary<string, ModFile>> localModFiles,
            Dictionary<string, Dictionary<string, ModFile>> remoteModFiles,
            Dictionary<string, Dictionary<string, ModFile>> previousRemoteModFiles
        )
        {
            return syncPaths
                .Select(
                    (syncPath) =>
                    {
                        var query = remoteModFiles[syncPath.path].Keys.Intersect(localModFiles[syncPath.path].Keys, StringComparer.OrdinalIgnoreCase);

                        if (!syncPath.enforced)
                            query = query
                                .Where((file) => !localModFiles[syncPath.path][file].nosync)
                                .Where(
                                    (file) =>
                                        !previousRemoteModFiles.TryGetValue(syncPath.path, out var previousSyncPath)
                                        || !previousSyncPath.TryGetValue(file, out var modFile)
                                        || remoteModFiles[syncPath.path][file].crc != modFile.crc
                                );

                        query = query
                            .Where((file) => !remoteModFiles[syncPath.path][file].nosync)
                            .Where((file) => remoteModFiles[syncPath.path][file].crc != localModFiles[syncPath.path][file].crc);

                        return new KeyValuePair<string, List<string>>(syncPath.path, query.ToList());
                    }
                )
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public static Dictionary<string, List<string>> GetRemovedFiles(
            List<SyncPath> syncPaths,
            Dictionary<string, Dictionary<string, ModFile>> localModFiles,
            Dictionary<string, Dictionary<string, ModFile>> remoteModFiles,
            Dictionary<string, Dictionary<string, ModFile>> previousRemoteModFiles
        )
        {
            return syncPaths
                .Select(
                    (syncPath) =>
                    {
                        IEnumerable<string> query;
                        if (syncPath.enforced)
                            query = localModFiles[syncPath.path].Keys.Except(remoteModFiles[syncPath.path].Keys, StringComparer.OrdinalIgnoreCase);
                        else
                            query = !previousRemoteModFiles.TryGetValue(syncPath.path, out var file)
                                ? []
                                : file
                                    .Keys.Intersect(localModFiles[syncPath.path].Keys, StringComparer.OrdinalIgnoreCase)
                                    .Except(remoteModFiles[syncPath.path].Keys, StringComparer.OrdinalIgnoreCase);

                        return new KeyValuePair<string, List<string>>(syncPath.path, query.ToList());
                    }
                )
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public static Dictionary<string, Dictionary<string, ModFile>> HashLocalFiles(string basePath, List<SyncPath> syncPaths, List<SyncPath> enabledSyncPaths)
        {
            return syncPaths
                .Select(
                    (syncPath) =>
                    {
                        var path = Path.Combine(basePath, syncPath.path);

                        var modFiles = new Dictionary<string, ModFile>();

                        if (File.Exists(path)) // Sync Path is a single file
                            modFiles = CreateModFile(basePath, path, enabledSyncPaths.Any((sp) => sp.path == syncPath.path))
                                .ToEnumerable()
                                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                        else if (Directory.Exists(path)) // Sync Path is a directory
                        {
                            modFiles = Utility
                                .GetFilesInDir(path)
                                .AsParallel()
                                .Where((file) => file != @"BepInEx\patchers\Corter-ModSync-Patcher.dll")
                                .Where((file) => !file.EndsWith(".nosync") && !file.EndsWith(".nosync.txt"))
                                .Select((file) => CreateModFile(basePath, file, enabledSyncPaths.Any((sp) => sp.path == syncPath.path)))
                                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
                        }

                        return new KeyValuePair<string, Dictionary<string, ModFile>>(syncPath.path, modFiles);
                    }
                )
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        }

        public static KeyValuePair<string, ModFile> CreateModFile(string basePath, string file, bool enabled)
        {
            var data = VFS.ReadFile(file);
            var relativePath = file.Replace($"{basePath}\\", "");

            return new KeyValuePair<string, ModFile>(relativePath, new ModFile(Crc32.Compute(data), !enabled || Utility.NoSyncInTree(basePath, relativePath)));
        }

        public static void CompareModFiles(
            List<SyncPath> syncPaths,
            Dictionary<string, Dictionary<string, ModFile>> localModFiles,
            Dictionary<string, Dictionary<string, ModFile>> remoteModFiles,
            Dictionary<string, Dictionary<string, ModFile>> previousSync,
            out Dictionary<string, List<string>> addedFiles,
            out Dictionary<string, List<string>> updatedFiles,
            out Dictionary<string, List<string>> removedFiles
        )
        {
            addedFiles = GetAddedFiles(syncPaths, localModFiles, remoteModFiles);
            updatedFiles = GetUpdatedFiles(syncPaths, localModFiles, remoteModFiles, previousSync);
            removedFiles = GetRemovedFiles(syncPaths, localModFiles, remoteModFiles, previousSync);
        }
    }
}
