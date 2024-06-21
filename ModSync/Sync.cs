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
            return remoteModFiles.Keys.Except(localModFiles.Keys).ToList();
        }

        public static List<string> GetUpdatedFiles(
            Dictionary<string, ModFile> localModFiles,
            Dictionary<string, ModFile> remoteModFiles,
            Dictionary<string, ModFile> previousRemoteModFiles
        )
        {
            var intersection = localModFiles.Keys.Intersect(remoteModFiles.Keys);

            if (previousRemoteModFiles.Count > 0)
                intersection.Intersect(previousRemoteModFiles.Keys);

            return intersection
                .Where((key) => !localModFiles[key].nosync)
                .Where((key) => remoteModFiles[key].crc != localModFiles[key].crc)
                .Where((key) => !previousRemoteModFiles.ContainsKey(key) || remoteModFiles[key].modified > previousRemoteModFiles[key].modified)
                .ToList();
        }

        public static List<string> GetRemovedFiles(
            Dictionary<string, ModFile> localModFiles,
            Dictionary<string, ModFile> remoteModFiles,
            Dictionary<string, ModFile> previousRemoteModFiles
        )
        {
            return previousRemoteModFiles.Keys.Intersect(localModFiles.Keys).Except(remoteModFiles.Keys).ToList();
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
                .ToDictionary(item => item.Key, item => item.Value);
        }

        public static KeyValuePair<string, ModFile> CreateModFile(string basePath, string file)
        {
            var data = VFS.ReadFile(file);
            var relativePath = file.Replace($"{basePath}\\", "");

            return new KeyValuePair<string, ModFile>(
                relativePath,
                new ModFile(
                    Crc32.Compute(data),
                    ((DateTimeOffset)File.GetLastWriteTimeUtc(file)).ToUnixTimeMilliseconds(),
                    Utility.NoSyncInTree(basePath, relativePath)
                )
            );
        }
    }
}
