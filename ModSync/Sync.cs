using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aki.Common.Utils;
using Aki.Custom.Utils;

namespace ModSync
{
    public static class Sync
    {
        public static List<string> GetAddedFiles(Dictionary<string, ModFile> localModFiles, Dictionary<string, ModFile> remoteModFiles)
        {
            return remoteModFiles.Keys.Except(localModFiles.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static List<string> GetUpdatedFiles(
            Dictionary<string, ModFile> localModFiles,
            Dictionary<string, ModFile> remoteModFiles,
            Dictionary<string, ModFile> previousRemoteModFiles
        )
        {
            var intersection = remoteModFiles.Keys.Intersect(localModFiles.Keys, StringComparer.OrdinalIgnoreCase);

            if (previousRemoteModFiles.Count > 0)
                intersection.Intersect(previousRemoteModFiles.Keys, StringComparer.OrdinalIgnoreCase);

            return intersection
                .Where((key) => !localModFiles[key].nosync)
                .Where((key) => previousRemoteModFiles.ContainsKey(key) && remoteModFiles[key].crc != previousRemoteModFiles[key].crc)
                .ToList();
        }

        public static List<string> GetRemovedFiles(
            Dictionary<string, ModFile> localModFiles,
            Dictionary<string, ModFile> remoteModFiles,
            Dictionary<string, ModFile> previousRemoteModFiles
        )
        {
            return previousRemoteModFiles
                .Keys.Intersect(localModFiles.Keys, StringComparer.OrdinalIgnoreCase)
                .Except(remoteModFiles.Keys, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static Dictionary<string, ModFile> HashLocalFiles(string basePath, List<string> enabledSyncPaths)
        {
            return enabledSyncPaths
                .Where((syncPath) => VFS.Exists(Path.Combine(basePath, syncPath)))
                .Select((subDir) => Path.Combine(basePath, subDir))
                .SelectMany(
                    (path) =>
                        Utility
                            .GetFilesInDir(path)
                            .AsParallel()
                            .Where((file) => !file.EndsWith(".nosync") && !file.EndsWith(".nosync.txt"))
                            .Select((file) => CreateModFile(basePath, file))
                )
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        }

        public static KeyValuePair<string, ModFile> CreateModFile(string basePath, string file)
        {
            var data = VFS.ReadFile(file);
            var relativePath = file.Replace($"{basePath}\\", "");

            return new KeyValuePair<string, ModFile>(relativePath, new ModFile(Crc32.Compute(data), !enabled || Utility.NoSyncInTree(basePath, relativePath)));
        }

        public static void CompareModFiles(
            Dictionary<string, ModFile> localModFiles,
            Dictionary<string, ModFile> remoteModFiles,
            Dictionary<string, ModFile> previousSync,
            out List<string> addedFiles,
            out List<string> updatedFiles,
            out List<string> removedFiles
        )
        {
            addedFiles = GetAddedFiles(localModFiles, remoteModFiles);
            updatedFiles = GetUpdatedFiles(localModFiles, remoteModFiles, previousSync);
            removedFiles = GetRemovedFiles(localModFiles, remoteModFiles, previousSync);
        }
    }
}
