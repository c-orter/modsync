using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace ModSync.Tests;

using SyncPathModFiles = Dictionary<string, Dictionary<string, ModFile>>;

[TestClass]
public class MigratorTests
{
    private static void CopyFilesRecursively(string source, string target) => CopyFilesRecursively(new DirectoryInfo(source), new DirectoryInfo(target));

    private static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
    {
        foreach (var dir in source.GetDirectories())
            CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
        foreach (var file in source.GetFiles())
            file.CopyTo(Path.Combine(target.FullName, file.Name), true);
    }

    [TestMethod]
    public void TestMigrateNoModSync()
    {
        var sourceDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\MigratorTests", "NoModSync"));
        var testDirectory = TestUtils.GetTemporaryDirectory();

        CopyFilesRecursively(sourceDirectory, testDirectory);

        List<SyncPath> syncPaths = [new SyncPath(@"BepInEx\plugins"), new SyncPath(@"BepInEx\patchers")];

        var migrator = new Migrator(testDirectory);
        migrator.TryMigrate(Version.Parse("0.8.0"), syncPaths);

        Assert.AreEqual(0, Directory.GetFiles(testDirectory, "*", SearchOption.AllDirectories).Length);

        Directory.Delete(testDirectory, true);
    }

    [TestMethod]
    public void TestMigrateOldModSyncFile()
    {
        var sourceDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\MigratorTests", "OldModSyncFile"));
        var testDirectory = TestUtils.GetTemporaryDirectory();

        CopyFilesRecursively(sourceDirectory, testDirectory);

        List<SyncPath> syncPaths = [new SyncPath(@"BepInEx\plugins"), new SyncPath(@"BepInEx\patchers")];

        var migrator = new Migrator(testDirectory);
        migrator.TryMigrate(Version.Parse("0.8.0"), syncPaths);

        Assert.AreEqual(0, Directory.GetFiles(testDirectory, "*", SearchOption.AllDirectories).Length);

        Directory.Delete(testDirectory, true);
    }

    [TestMethod]
    public void TestMigrateVersionedModSyncFile()
    {
        var sourceDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\MigratorTests", "VersionedModSyncFile"));
        var testDirectory = TestUtils.GetTemporaryDirectory();

        CopyFilesRecursively(sourceDirectory, testDirectory);

        List<SyncPath> syncPaths = [new SyncPath(@"BepInEx\plugins"), new SyncPath(@"BepInEx\patchers")];

        var migrator = new Migrator(testDirectory);
        migrator.TryMigrate(Version.Parse("0.8.0"), syncPaths);

        var modSyncDir = Path.Combine(testDirectory, "ModSync_Data");

        Assert.IsFalse(File.Exists(Path.Combine(testDirectory, ".modsync")));
        Assert.IsTrue(Directory.Exists(modSyncDir));
        Assert.IsTrue(File.Exists(Path.Combine(modSyncDir, "PreviousSync.json")));

        var previousSync = JsonConvert.DeserializeObject<SyncPathModFiles>(File.ReadAllText(Path.Combine(modSyncDir, "PreviousSync.json")));

        CollectionAssert.AreEquivalent(syncPaths.Select(syncPath => syncPath.path).ToList(), previousSync.Keys);
        CollectionAssert.AreEquivalent(
            new List<string> { @"BepInEx\plugins\SAIN.dll", @"BepInEx\plugins\Corter-ModSync.dll" },
            previousSync[@"BepInEx\plugins"].Keys
        );

        Assert.AreEqual(1234567u, previousSync[@"BepInEx\plugins"][@"BepInEx\plugins\SAIN.dll"].crc);
        Assert.AreEqual(1234567u, previousSync[@"BepInEx\plugins"][@"BepInEx\plugins\Corter-ModSync.dll"].crc);

        Assert.IsTrue(File.Exists(Path.Combine(modSyncDir, "Version.txt")));
        Assert.AreEqual("0.8.0", File.ReadAllText(Path.Combine(modSyncDir, "Version.txt")));

        Directory.Delete(testDirectory, true);
    }

    [TestMethod]
    public void TestMigrateModSyncDirectory()
    {
        var sourceDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\MigratorTests", "ModSyncDirectory"));
        var testDirectory = TestUtils.GetTemporaryDirectory();

        CopyFilesRecursively(sourceDirectory, testDirectory);

        List<SyncPath> syncPaths = [new SyncPath(@"BepInEx\plugins"), new SyncPath(@"BepInEx\patchers")];

        var migrator = new Migrator(testDirectory);
        migrator.TryMigrate(Version.Parse("0.8.0"), syncPaths);

        CollectionAssert.AreEquivalent(
            Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories).Select(file => file.Replace(sourceDirectory, "")).ToList(),
            Directory.GetFiles(testDirectory, "*", SearchOption.AllDirectories).Select(file => file.Replace(testDirectory, "")).ToList()
        );

        var modSyncDir = Path.Combine(testDirectory, "ModSync_Data");
        Assert.AreEqual("0.8.0", File.ReadAllText(Path.Combine(modSyncDir, "Version.txt")));

        var sourcePreviousSync = File.ReadAllText(Path.Combine(sourceDirectory, "ModSync_Data", "PreviousSync.json"));
        var testPreviousSync = File.ReadAllText(Path.Combine(testDirectory, "ModSync_Data", "PreviousSync.json"));

        Assert.AreEqual(sourcePreviousSync, testPreviousSync);
    }
}
