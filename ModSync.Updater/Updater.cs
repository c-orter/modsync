using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ModSync.Updater;

public static class Updater
{
    private static void MoveFilesRecursively(string source, string target) => MoveFilesRecursively(new DirectoryInfo(source), new DirectoryInfo(target));

    private static void MoveFilesRecursively(DirectoryInfo source, DirectoryInfo target)
    {
        foreach (var dir in source.GetDirectories())
            MoveFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
        foreach (var file in source.GetFiles())
        {
            Logger.Log($"Copying file: {Path.Combine(target.FullName, file.Name)}");
            file.MoveTo(Path.Combine(target.FullName, file.Name), true);
        }
    }

    public static void ReplaceUpdatedFiles()
    {
        if (!Directory.Exists(Program.UPDATE_DIR))
            return;

        MoveFilesRecursively(Program.UPDATE_DIR, Directory.GetCurrentDirectory());
        Logger.Log($"Deleting update directory: {Program.UPDATE_DIR}");
        Directory.Delete(Program.UPDATE_DIR, true);
    }

    public static void DeleteRemovedFiles()
    {
        if (!File.Exists(Program.REMOVED_FILES_PATH))
            return;

        var filesToDelete = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Program.REMOVED_FILES_PATH)) ?? [];

        foreach (var file in filesToDelete)
        {
            if (Path.IsPathRooted(file))
                throw new Exception("[Corter-ModSync Updater]: Paths to delete cannot be absolute.");

            if (!Path.GetFullPath(file).StartsWith(Directory.GetCurrentDirectory()))
                throw new Exception("[Corter-ModSync Updater]: Path to delete is not relative to the current directory.");

            if (!File.Exists(file))
                continue;

            if (File.Exists(file))
            {
                Logger.Log($"Deleting file: {file}");
                File.Delete(file);
            }

            if (Directory.GetParent(file)!.GetFiles("*", SearchOption.AllDirectories).Length == 0)
            {
                Logger.Log($"Deleting directory: {Directory.GetParent(file)!.FullName}");
                Directory.GetParent(file)!.Delete();
            }
        }

        Logger.Log($"Deleting removed files list: {Program.REMOVED_FILES_PATH}");
        File.Delete(Program.REMOVED_FILES_PATH);
    }
}
