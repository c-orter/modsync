using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;

namespace ModSync.PrePatcher
{
    public static class Patcher
    {
        // List of assemblies to patch
        public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" };

        public static void CopyFilesRecursively(string source, string target) => CopyFilesRecursively(new DirectoryInfo(source), new DirectoryInfo(target));

        public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
            foreach (FileInfo file in source.GetFiles())
                file.CopyTo(Path.Combine(target.FullName, file.Name), true);
        }

        // Patches the assemblies
        public static void Patch(AssemblyDefinition assembly)
        {
            // Patcher code here
            var pendingUpdatePath = Path.Combine(Directory.GetCurrentDirectory(), ".pending-update");

            if (File.Exists(pendingUpdatePath))
            {
                var updateDir = File.ReadAllText(pendingUpdatePath).Trim();

                var clientModsDir = Path.Combine(updateDir, "clientMods");
                if (Directory.Exists(clientModsDir))
                    CopyFilesRecursively(clientModsDir, Directory.GetCurrentDirectory());

                var serverModsDir = Path.Combine(updateDir, "serverMods");
                if (Directory.Exists(serverModsDir))
                    CopyFilesRecursively(serverModsDir, Directory.GetCurrentDirectory());

                Directory.Delete(updateDir, true);
                File.Delete(pendingUpdatePath);
            }
        }
    }
}
