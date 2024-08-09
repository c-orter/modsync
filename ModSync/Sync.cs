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
            return syncPaths
                .Select(
                    (syncPath) =>
                        new KeyValuePair<string, List<string>>(
                            syncPath.path,
                            remoteModFiles[syncPath.path]
                                .Keys.Where((file) => !remoteModFiles[syncPath.path][file].nosync)
                                .Except(
                                    localModFiles.TryGetValue(syncPath.path, out var modFiles) ? modFiles.Keys : new List<string>(),
                                    StringComparer.OrdinalIgnoreCase
                                )
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
                        if (!localModFiles.TryGetValue(syncPath.path, out var localPathFiles))
                            return new KeyValuePair<string, List<string>>(syncPath.path, []);

                        var query = remoteModFiles[syncPath.path].Keys.Intersect(localPathFiles.Keys, StringComparer.OrdinalIgnoreCase);

                        if (!syncPath.enforced)
                            query = query
                                .Where((file) => !localPathFiles[file].nosync)
                                .Where(
                                    (file) =>
                                        !previousRemoteModFiles.TryGetValue(syncPath.path, out var previousPathFiles)
                                        || !previousPathFiles.TryGetValue(file, out var modFile)
                                        || remoteModFiles[syncPath.path][file].crc != modFile.crc
                                );

                        query = query
                            .Where((file) => !remoteModFiles[syncPath.path][file].nosync)
                            .Where((file) => remoteModFiles[syncPath.path][file].crc != localPathFiles[file].crc);

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
                        if (!localModFiles.TryGetValue(syncPath.path, out var localPathFiles))
                            return new KeyValuePair<string, List<string>>(syncPath.path, []);

                        IEnumerable<string> query;
                        if (syncPath.enforced)
                            query = localPathFiles.Keys.Except(remoteModFiles[syncPath.path].Keys, StringComparer.OrdinalIgnoreCase);
                        else
                            query = !previousRemoteModFiles.TryGetValue(syncPath.path, out var previousPathFiles)
                                ? []
                                : previousPathFiles
                                    .Keys.Intersect(localPathFiles.Keys, StringComparer.OrdinalIgnoreCase)
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
                            modFiles = Directory
                                .GetFiles(path, "*", SearchOption.AllDirectories)
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

            return new KeyValuePair<string, ModFile>(relativePath, new ModFile(Crc32.Compute(data), !enabled || NoSyncInTree(basePath, relativePath)));
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

        public static bool NoSyncInTree(string baseDir, string path)
        {
            if (path == baseDir || path == string.Empty)
                return false;

            var file = Path.Combine(baseDir, path);
            if (File.Exists(file))
            {
                if (VFS.Exists($"{file}.nosync") || VFS.Exists($"{file}.nosync.txt"))
                    return true;
            }
            else if (Directory.Exists(Path.Combine(baseDir, path)))
            {
                var noSyncPath = Path.Combine(baseDir, path, ".nosync");

                if (VFS.Exists(noSyncPath) || VFS.Exists($"{noSyncPath}.txt"))
                    return true;
            }
            else
                return false;

            return NoSyncInTree(baseDir, Path.GetDirectoryName(path));
        }
    }
}
