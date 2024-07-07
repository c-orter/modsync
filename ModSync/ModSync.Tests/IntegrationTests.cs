extern alias patcher;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aki.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ModSync.Tests
{
    [TestClass]
    public class IntegrationTests
    {
        private static Persist RunPlugin(
            string testPath,
            List<string> syncPaths,
            bool configDeleteRemovedFiles,
            out List<string> addedFiles,
            out List<string> updatedFiles,
            out List<string> removedFiles,
            ref List<string> downloadedFiles
        )
        {
            var remotePath = Path.Combine(testPath, "remote");
            var localPath = Path.Combine(testPath, "local");

            var persistPath = Path.Combine(localPath, ".modsync");
            var oldPersist = VFS.Exists(persistPath) ? Json.Deserialize<Persist>(File.ReadAllText(persistPath)) : new();

            var remoteModFiles = Sync.HashLocalFiles(remotePath, syncPaths, syncPaths);
            var localModFiles = Sync.HashLocalFiles(localPath, syncPaths, syncPaths);

            Sync.CompareModFiles(localModFiles, remoteModFiles, oldPersist.previousSync, out addedFiles, out updatedFiles, out removedFiles);

            downloadedFiles.AddRange(addedFiles.Union(updatedFiles));

            Persist newPersist =
                new()
                {
                    previousSync = remoteModFiles,
                    downloadDir = localPath,
                    filesToDelete = configDeleteRemovedFiles ? removedFiles : []
                };

            return newPersist;
        }

        [TestMethod]
        public void TestInitialEmptySingleFile()
        {
            var testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\IntegrationTests", "InitialEmptySingleFile"));

            List<string> downloadedFiles = [];

            var persist = RunPlugin(
                testPath,
                syncPaths: ["SAIN.dll"],
                configDeleteRemovedFiles: true,
                out var addedFiles,
                out var updatedFiles,
                out var removedFiles,
                ref downloadedFiles
            );

            Assert.AreEqual(1, addedFiles.Count);
            Assert.AreEqual(0, updatedFiles.Count);
            Assert.AreEqual(0, removedFiles.Count);

            Assert.AreEqual(1, downloadedFiles.Count);
            Assert.IsTrue(downloadedFiles.Contains("SAIN.dll"));

            Assert.AreEqual(0, persist.filesToDelete.Count);

            Assert.AreEqual(1, persist.previousSync.Count);
            Assert.IsTrue(persist.previousSync.ContainsKey("SAIN.dll"));
        }

        [TestMethod]
        public void TestInitialEmptyManyFiles()
        {
            var testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\IntegrationTests", "InitialEmptyManyFiles"));

            List<string> downloadedFiles = [];

            var persist = RunPlugin(
                testPath,
                syncPaths: ["plugins"],
                configDeleteRemovedFiles: true,
                out var addedFiles,
                out var updatedFiles,
                out var removedFiles,
                ref downloadedFiles
            );

            Assert.AreEqual(2, addedFiles.Count);
            Assert.AreEqual(0, updatedFiles.Count);
            Assert.AreEqual(0, removedFiles.Count);

            Assert.AreEqual(2, downloadedFiles.Count);
            CollectionAssert.AreEquivalent(new List<string>() { @"plugins\SAIN.dll", @"plugins\Corter-ModSync.dll" }, downloadedFiles);

            Assert.AreEqual(0, persist.filesToDelete.Count);

            Assert.AreEqual(2, persist.previousSync.Count);
            CollectionAssert.AreEquivalent(new List<string>() { @"plugins\SAIN.dll", @"plugins\Corter-ModSync.dll" }, persist.previousSync.Keys);
        }

        [TestMethod]
        public void TestUpdateSingleFile()
        {
            var testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\IntegrationTests", "UpdateSingleFile"));

            List<string> downloadedFiles = [];

            var persist = RunPlugin(
                testPath,
                syncPaths: ["SAIN.dll"],
                configDeleteRemovedFiles: true,
                out var addedFiles,
                out var updatedFiles,
                out var removedFiles,
                ref downloadedFiles
            );

            Assert.AreEqual(0, addedFiles.Count);
            Assert.AreEqual(1, updatedFiles.Count);
            Assert.AreEqual(0, removedFiles.Count);

            Assert.AreEqual(1, downloadedFiles.Count);
            Assert.IsTrue(downloadedFiles.Contains("SAIN.dll"));

            Assert.AreEqual(0, persist.filesToDelete.Count);

            Assert.AreEqual(1, persist.previousSync.Count);
            Assert.IsTrue(persist.previousSync.ContainsKey("SAIN.dll"));
        }

        [TestMethod]
        public void TestDoNotUpdateWhenLocalChanges()
        {
            var testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\IntegrationTests", "DoNotUpdateWhenLocalChanges"));

            List<string> downloadedFiles = [];

            var persist = RunPlugin(
                testPath,
                syncPaths: ["SAIN.dll"],
                configDeleteRemovedFiles: true,
                out var addedFiles,
                out var updatedFiles,
                out var removedFiles,
                ref downloadedFiles
            );

            Assert.AreEqual(0, addedFiles.Count);
            Assert.AreEqual(0, updatedFiles.Count);
            Assert.AreEqual(0, removedFiles.Count);

            Assert.AreEqual(0, downloadedFiles.Count);
            Assert.AreEqual(0u, persist.previousSync["SAIN.dll"].crc);
        }

        [TestMethod]
        public void TestRemoveSingleFile()
        {
            var testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\IntegrationTests", "RemoveSingleFile"));

            List<string> downloadedFiles = [];

            var persist = RunPlugin(
                testPath,
                syncPaths: ["SAIN.dll"],
                configDeleteRemovedFiles: true,
                out var addedFiles,
                out var updatedFiles,
                out var removedFiles,
                ref downloadedFiles
            );

            Assert.AreEqual(0, addedFiles.Count);
            Assert.AreEqual(0, updatedFiles.Count);
            Assert.AreEqual(1, removedFiles.Count);

            Assert.AreEqual(0, downloadedFiles.Count);
            CollectionAssert.AreEquivalent(new List<string>() { "SAIN.dll" }, persist.filesToDelete);
        }

        [TestMethod]
        public void TestMismatchedCases()
        {
            var testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\IntegrationTests", "MismatchedCases"));

            List<string> downloadedFiles = [];

            var persist = RunPlugin(
                testPath,
                syncPaths: ["plugins"],
                configDeleteRemovedFiles: true,
                out var addedFiles,
                out var updatedFiles,
                out var removedFiles,
                ref downloadedFiles
            );

            Assert.AreEqual(0, addedFiles.Count);
            Assert.AreEqual(0, updatedFiles.Count);
            Assert.AreEqual(0, removedFiles.Count);

            Assert.AreEqual(1, downloadedFiles.Count);
            Assert.AreEqual(@"plugins\sain.dll", downloadedFiles[0]);
        }
    }
}
