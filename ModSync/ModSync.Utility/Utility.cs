using System.Collections.Generic;
using System.IO;
using Aki.Common.Utils;

namespace ModSync
{
    public static class Utility
    {
        public static List<string> GetFilesInDir(string dir)
        {
            if (File.Exists(dir))
                return [dir];

            var files = new List<string>();

            foreach (var directory in VFS.GetDirectories(dir))
            {
                foreach (var file in GetFilesInDir(directory))
                    files.Add(file);
            }

            foreach (var file in VFS.GetFiles(dir))
                files.Add(file);

            return files;
        }

        public static bool NoSyncInTree(string baseDir, string path)
        {
            if (path == baseDir || path == string.Empty)
                return false;

            string file = Path.Combine(baseDir, path);
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

        public static string GetTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            if (File.Exists(tempDirectory))
                return GetTemporaryDirectory();
            else
            {
                Directory.CreateDirectory(tempDirectory);
                return tempDirectory;
            }
        }

        public static void CopyFilesRecursively(string source, string target, bool overwrite = false) =>
            CopyFilesRecursively(new DirectoryInfo(source), new DirectoryInfo(target), overwrite);

        public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target, bool overwrite = false)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name), overwrite);
            foreach (FileInfo file in source.GetFiles())
                file.CopyTo(Path.Combine(target.FullName, file.Name), overwrite);
        }
    }
}
