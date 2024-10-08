﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SPT.Common.Utils;

namespace ModSync.Tests;

using SyncPathFileList = Dictionary<string, List<string>>;
using SyncPathModFiles = Dictionary<string, Dictionary<string, ModFile>>;

[TestClass]
public class IntegrationTests
{
    private static (SyncPathModFiles, List<string>) RunPlugin(
        string testPath,
        List<SyncPath> syncPaths,
        bool configDeleteRemovedFiles,
        out SyncPathFileList addedFiles,
        out SyncPathFileList updatedFiles,
        out SyncPathFileList removedFiles,
        ref List<string> downloadedFiles
    )
    {
        var remotePath = Path.Combine(testPath, "remote");
        if (!Directory.Exists(remotePath))
            Directory.CreateDirectory(remotePath);

        var localPath = Path.Combine(testPath, "local");
        if (!Directory.Exists(localPath))
            Directory.CreateDirectory(localPath);

        var previousSyncPath = Path.Combine(localPath, "ModSync_Data", "PreviousSync.json");
        var previousSync = VFS.Exists(previousSyncPath) ? Json.Deserialize<SyncPathModFiles>(File.ReadAllText(previousSyncPath)) : [];

        var remoteModFiles = Sync.HashLocalFiles(remotePath, syncPaths, syncPaths);
        var localModFiles = Sync.HashLocalFiles(localPath, syncPaths, syncPaths);

        Sync.CompareModFiles(syncPaths, localModFiles, remoteModFiles, previousSync, out addedFiles, out updatedFiles, out removedFiles);

        downloadedFiles.AddRange(addedFiles.SelectMany(kvp => kvp.Value).Union(updatedFiles.SelectMany(kvp => kvp.Value)));

        return (remoteModFiles, configDeleteRemovedFiles ? removedFiles.SelectMany(kvp => kvp.Value).ToList() : []);
    }

    [TestMethod]
    public void TestInitialEmptySingleFile()
    {
        var testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\IntegrationTests", "InitialEmptySingleFile"));

        List<string> downloadedFiles = [];

        var (previousSync, filesToDelete) = RunPlugin(
            testPath,
            syncPaths: [new SyncPath("SAIN.dll")],
            configDeleteRemovedFiles: true,
            out var addedFiles,
            out var updatedFiles,
            out var removedFiles,
            ref downloadedFiles
        );

        Assert.AreEqual(1, addedFiles["SAIN.dll"].Count);
        Assert.AreEqual(0, updatedFiles["SAIN.dll"].Count);
        Assert.AreEqual(0, removedFiles["SAIN.dll"].Count);

        Assert.AreEqual(1, downloadedFiles.Count);
        Assert.IsTrue(downloadedFiles.Contains("SAIN.dll"));

        Assert.AreEqual(0, filesToDelete.Count);

        Assert.AreEqual(1, previousSync["SAIN.dll"].Count);
        Assert.IsTrue(previousSync.ContainsKey("SAIN.dll"));
    }

    [TestMethod]
    public void TestInitialEmptyManyFiles()
    {
        var testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\IntegrationTests", "InitialEmptyManyFiles"));

        List<string> downloadedFiles = [];

        var (previousSync, filesToDelete) = RunPlugin(
            testPath,
            syncPaths: [new SyncPath("plugins")],
            configDeleteRemovedFiles: true,
            out var addedFiles,
            out var updatedFiles,
            out var removedFiles,
            ref downloadedFiles
        );

        Assert.AreEqual(2, addedFiles["plugins"].Count);
        Assert.AreEqual(0, updatedFiles["plugins"].Count);
        Assert.AreEqual(0, removedFiles["plugins"].Count);

        Assert.AreEqual(2, downloadedFiles.Count);
        CollectionAssert.AreEquivalent(new List<string> { @"plugins\SAIN.dll", @"plugins\Corter-ModSync.dll" }, downloadedFiles);

        Assert.AreEqual(0, filesToDelete.Count);

        Assert.AreEqual(2, previousSync["plugins"].Count);
        CollectionAssert.AreEquivalent(new List<string> { @"plugins\SAIN.dll", @"plugins\Corter-ModSync.dll" }, previousSync["plugins"].Keys);
    }

    [TestMethod]
    public void TestUpdateSingleFile()
    {
        var testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\IntegrationTests", "UpdateSingleFile"));

        List<string> downloadedFiles = [];

        var (previousSync, filesToDelete) = RunPlugin(
            testPath,
            syncPaths: [new SyncPath("SAIN.dll")],
            configDeleteRemovedFiles: true,
            out var addedFiles,
            out var updatedFiles,
            out var removedFiles,
            ref downloadedFiles
        );

        Assert.AreEqual(0, addedFiles["SAIN.dll"].Count);
        Assert.AreEqual(1, updatedFiles["SAIN.dll"].Count);
        Assert.AreEqual(0, removedFiles["SAIN.dll"].Count);

        Assert.AreEqual(1, downloadedFiles.Count);
        Assert.IsTrue(downloadedFiles.Contains("SAIN.dll"));

        Assert.AreEqual(0, filesToDelete.Count);

        Assert.AreEqual(1, previousSync["SAIN.dll"].Count);
        Assert.IsTrue(previousSync["SAIN.dll"].ContainsKey("SAIN.dll"));
    }

    [TestMethod]
    public void TestDoNotUpdateWhenLocalChanges()
    {
        var testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\IntegrationTests", "DoNotUpdateWhenLocalChanges"));

        List<string> downloadedFiles = [];

        var (previousSync, _) = RunPlugin(
            testPath,
            syncPaths: [new SyncPath("SAIN.dll")],
            configDeleteRemovedFiles: true,
            out var addedFiles,
            out var updatedFiles,
            out var removedFiles,
            ref downloadedFiles
        );

        Assert.AreEqual(0, addedFiles["SAIN.dll"].Count);
        Assert.AreEqual(0, updatedFiles["SAIN.dll"].Count);
        Assert.AreEqual(0, removedFiles["SAIN.dll"].Count);

        Assert.AreEqual(0, downloadedFiles.Count);
        Assert.AreEqual(0u, previousSync["SAIN.dll"]["SAIN.dll"].crc);
    }

    [TestMethod]
    public void TestRemoveSingleFile()
    {
        var testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\IntegrationTests", "RemoveSingleFile"));

        List<string> downloadedFiles = [];

        var (_, filesToDelete) = RunPlugin(
            testPath,
            syncPaths: [new SyncPath("SAIN.dll")],
            configDeleteRemovedFiles: true,
            out var addedFiles,
            out var updatedFiles,
            out var removedFiles,
            ref downloadedFiles
        );

        Assert.AreEqual(0, addedFiles["SAIN.dll"].Count);
        Assert.AreEqual(0, updatedFiles["SAIN.dll"].Count);
        Assert.AreEqual(1, removedFiles["SAIN.dll"].Count);

        Assert.AreEqual(0, downloadedFiles.Count);
        CollectionAssert.AreEquivalent(new List<string> { "SAIN.dll" }, filesToDelete);
    }

    [TestMethod]
    public void TestMismatchedCases()
    {
        var testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\IntegrationTests", "MismatchedCases"));

        List<string> downloadedFiles = [];

        RunPlugin(
            testPath,
            syncPaths: [new SyncPath("plugins")],
            configDeleteRemovedFiles: true,
            out var addedFiles,
            out var updatedFiles,
            out var removedFiles,
            ref downloadedFiles
        );

        Assert.AreEqual(0, addedFiles["plugins"].Count);
        Assert.AreEqual(1, updatedFiles["plugins"].Count);
        Assert.AreEqual(0, removedFiles["plugins"].Count);

        Assert.AreEqual(1, downloadedFiles.Count);
        Assert.AreEqual(@"plugins\sain.dll", downloadedFiles[0]);
    }

    [TestMethod]
    public void TestClientNoSync()
    {
        var testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\IntegrationTests", "ClientNoSync"));

        List<string> downloadedFiles = [];

        var (_, filesToDelete) = RunPlugin(
            testPath,
            syncPaths: [new SyncPath("plugins")],
            configDeleteRemovedFiles: true,
            out var addedFiles,
            out var updatedFiles,
            out var removedFiles,
            ref downloadedFiles
        );

        Assert.AreEqual(0, addedFiles["plugins"].Count);
        Assert.AreEqual(0, updatedFiles["plugins"].Count);
        Assert.AreEqual(0, removedFiles["plugins"].Count);
        Assert.AreEqual(0, filesToDelete.Count);
    }
}
