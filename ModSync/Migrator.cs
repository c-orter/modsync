﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SPT.Common.Utils;

namespace ModSync;

public class Migrator(string baseDir)
{
    private string MODSYNC_DIR => Path.Combine(baseDir, "ModSync_Data");
    private string VERSION_PATH => Path.Combine(MODSYNC_DIR, "Version.txt");
    private string PREVIOUS_SYNC_PATH => Path.Combine(MODSYNC_DIR, "PreviousSync.json");
    private string MODSYNC_PATH => Path.Combine(baseDir, ".modsync");

    private List<string> CLEANUP_FILES => [MODSYNC_PATH, Path.Combine(baseDir, @"BepInEx\patchers\Corter-ModSync-Patcher.dll")];

    private Version DetectPreviousVersion()
    {
        try
        {
            if (Directory.Exists(MODSYNC_DIR) && File.Exists(VERSION_PATH))
                return Version.Parse(File.ReadAllText(VERSION_PATH));

            if (File.Exists(MODSYNC_PATH))
            {
                var persist = JObject.Parse(File.ReadAllText(MODSYNC_PATH));
                if (persist.ContainsKey("version") && persist["version"] != null)
                {
                    return persist["version"].Value<int>() switch
                    {
                        7 => Version.Parse("0.7.0"),
                        _ => Version.Parse("0.0.0")
                    };
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning("Failed to identify previous version. Cleaning up and attempting to continue.");
            Plugin.Logger.LogWarning(e);
        }

        return Version.Parse("0.0.0");
    }

    private void Cleanup()
    {
        if (Directory.Exists(MODSYNC_DIR))
            Directory.Delete(MODSYNC_DIR, true);

        foreach (var file in CLEANUP_FILES.Where(File.Exists))
            File.Delete(file);
    }

    public void TryMigrate(Version pluginVersion, List<SyncPath> syncPaths)
    {
        var oldVersion = DetectPreviousVersion();

        if (oldVersion == Version.Parse("0.0.0"))
        {
            Cleanup();
        }
        else if (oldVersion < Version.Parse("0.8.0") && oldVersion != pluginVersion)
        {
            var persist = JObject.Parse(File.ReadAllText(MODSYNC_PATH));

            if (!persist.ContainsKey("previousSync") || persist["previousSync"] == null)
            {
                Cleanup();
                return;
            }

            var oldPreviousSync = (JObject)persist["previousSync"];
            var newPreviousSync = syncPaths
                .Select(syncPath => new KeyValuePair<string, Dictionary<string, ModFile>>(syncPath.path, []))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            foreach (var property in oldPreviousSync.Properties())
            {
                var syncPath = syncPaths.Find(s => property.Name.StartsWith($"{s.path}\\"));
                if (syncPath == null)
                {
                    Plugin.Logger.LogWarning($"Could not migrate previous sync of '{property.Name}'. Does not match any current sync paths.");
                    continue;
                }

                var modFile = (JObject)property.Value;
                if (!modFile.ContainsKey("crc"))
                {
                    Plugin.Logger.LogWarning($"Could not migrate previous sync of '{property.Name}'. Does not contain crc.");
                    continue;
                }

                newPreviousSync[syncPath.path][property.Name] = new ModFile(modFile["crc"]!.Value<uint>(), modFile["nosync"]?.Value<bool>() ?? false);
            }

            if (!Directory.Exists(MODSYNC_DIR))
                Directory.CreateDirectory(MODSYNC_DIR);

            File.WriteAllText(PREVIOUS_SYNC_PATH, Json.Serialize(newPreviousSync));
            File.WriteAllText(VERSION_PATH, pluginVersion.ToString());

            foreach (var file in CLEANUP_FILES.Where(File.Exists))
                File.Delete(file);
        }
        else if (oldVersion.Minor == pluginVersion.Minor && oldVersion != pluginVersion)
        {
            Plugin.Logger.LogWarning("Previous sync was made with a different version of the plugin. This may cause issues. Continuing...");
        }
    }
}
