using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SPT.Common.Utils;
using Mono.Cecil;

namespace ModSync.PrePatcher
{
    public static class Patcher
    {
        public static IEnumerable<string> TargetDLLs { get; } = ["Assembly-CSharp.dll"];

        private static void Cleanup(string persistPath, Persist persist)
        {
            persist.downloadDir = string.Empty;
            persist.filesToDelete.Clear();

            if (persist.previousSync.Count > 0)
                File.WriteAllText(persistPath, Json.Serialize(persist));
        }

        public static void Patch(AssemblyDefinition assembly)
        {
            var persistPath = Path.Combine(Directory.GetCurrentDirectory(), ".modsync");

            if (!File.Exists(persistPath))
                return;

            var persist = Json.Deserialize<Persist>(File.ReadAllText(persistPath));

            if (persist.downloadDir != string.Empty)
            {
                if (!Directory.Exists(persist.downloadDir))
                {
                    Cleanup(persistPath, persist);
                    throw new Exception("Update directory was not found. Please try updating again from in-game!");
                }

                Utility.CopyFilesRecursively(persist.downloadDir, Directory.GetCurrentDirectory(), true);
                Directory.Delete(persist.downloadDir, true);
            }

            foreach (var file in persist.filesToDelete)
            {
                if (File.Exists(file))
                    File.Delete(file);

                if (!Directory.GetParent(file)!.GetFiles("*.*", SearchOption.AllDirectories).Any())
                    Directory.GetParent(file)!.Delete();
            }

            Cleanup(persistPath, persist);
        }
    }
}
