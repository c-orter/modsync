extern alias patcher;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aki.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ModSync
{
    [TestClass]
    public class IntegrationTests
    {
        private Persist RunPlugin(
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

            var remoteModFiles = Sync.HashLocalFiles(remotePath, syncPaths);
            var localModFiles = Sync.HashLocalFiles(localPath, syncPaths);

            Sync.CompareModFiles(localModFiles, remoteModFiles, oldPersist.previousSync, out addedFiles, out updatedFiles, out removedFiles);

            foreach (var file in addedFiles.Union(updatedFiles))
                downloadedFiles.Add(file);

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
            string testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\IntegrationTests", "InitialEmptySingleFile"));

            List<string> downloadedFiles = [];

            var persist = RunPlugin(
                testPath,
                syncPaths: ["SAIN.dll"],
                configDeleteRemovedFiles: true,
                out List<string> addedFiles,
                out List<string> updatedFiles,
                out List<string> removedFiles,
                ref downloadedFiles
            );

            Assert.AreEqual(addedFiles.Count, 1);
            Assert.AreEqual(updatedFiles.Count, 0);
            Assert.AreEqual(removedFiles.Count, 0);

            Assert.AreEqual(downloadedFiles.Count, 1);
            Assert.IsTrue(downloadedFiles.Contains("SAIN.dll"));

            Assert.AreEqual(persist.filesToDelete.Count, 0);

            Assert.AreEqual(persist.previousSync.Count, 1);
            Assert.IsTrue(persist.previousSync.ContainsKey("SAIN.dll"));
        }

        [TestMethod]
        public void TestInitialEmptyManyFiles()
        {
            string testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\IntegrationTests", "InitialEmptyManyFiles"));

            List<string> downloadedFiles = [];

            var persist = RunPlugin(
                testPath,
                syncPaths: ["plugins"],
                configDeleteRemovedFiles: true,
                out List<string> addedFiles,
                out List<string> updatedFiles,
                out List<string> removedFiles,
                ref downloadedFiles
            );

            Assert.AreEqual(addedFiles.Count, 2);
            Assert.AreEqual(updatedFiles.Count, 0);
            Assert.AreEqual(removedFiles.Count, 0);

            Assert.AreEqual(addedFiles.Count, 2);
            Assert.AreEqual(updatedFiles.Count, 0);
            Assert.AreEqual(removedFiles.Count, 0);

            Assert.AreEqual(downloadedFiles.Count, 2);
            CollectionAssert.AreEquivalent(downloadedFiles, new List<string>() { "plugins\\SAIN.dll", "plugins\\Corter-ModSync.dll" });

            Assert.AreEqual(persist.filesToDelete.Count, 0);

            Assert.AreEqual(persist.previousSync.Count, 2);
            CollectionAssert.AreEquivalent(persist.previousSync.Keys, new List<string>() { "plugins\\SAIN.dll", "plugins\\Corter-ModSync.dll" });
        }

        [TestMethod]
        public void TestUpdateSingleFile()
        {
            string testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\IntegrationTests", "UpdateSingleFile"));

            List<string> downloadedFiles = [];

            var persist = RunPlugin(
                testPath,
                syncPaths: ["SAIN.dll"],
                configDeleteRemovedFiles: true,
                out List<string> addedFiles,
                out List<string> updatedFiles,
                out List<string> removedFiles,
                ref downloadedFiles
            );

            Assert.AreEqual(addedFiles.Count, 0);
            Assert.AreEqual(updatedFiles.Count, 1);
            Assert.AreEqual(removedFiles.Count, 0);

            Assert.AreEqual(downloadedFiles.Count, 1);
            Assert.IsTrue(downloadedFiles.Contains("SAIN.dll"));

            Assert.AreEqual(persist.filesToDelete.Count, 0);

            Assert.AreEqual(persist.previousSync.Count, 1);
            Assert.IsTrue(persist.previousSync.ContainsKey("SAIN.dll"));
        }

        [TestMethod]
        public void TestDoNotUpdateWhenLocalChanges()
        {
            string testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\IntegrationTests", "DoNotUpdateWhenLocalChanges"));

            List<string> downloadedFiles = [];

            var persist = RunPlugin(
                testPath,
                syncPaths: ["SAIN.dll"],
                configDeleteRemovedFiles: true,
                out List<string> addedFiles,
                out List<string> updatedFiles,
                out List<string> removedFiles,
                ref downloadedFiles
            );

            Assert.AreEqual(addedFiles.Count, 0);
            Assert.AreEqual(updatedFiles.Count, 0);
            Assert.AreEqual(removedFiles.Count, 0);

            Assert.AreEqual(downloadedFiles.Count, 0);
            Assert.AreEqual(persist.previousSync["SAIN.dll"].crc, 0u);
        }

        [TestMethod]
        public void TestRemoveSingleFile()
        {
            string testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\IntegrationTests", "RemoveSingleFile"));

            List<string> downloadedFiles = [];

            var persist = RunPlugin(
                testPath,
                syncPaths: ["SAIN.dll"],
                configDeleteRemovedFiles: true,
                out List<string> addedFiles,
                out List<string> updatedFiles,
                out List<string> removedFiles,
                ref downloadedFiles
            );

            Assert.AreEqual(addedFiles.Count, 0);
            Assert.AreEqual(updatedFiles.Count, 0);
            Assert.AreEqual(removedFiles.Count, 1);

            Assert.AreEqual(downloadedFiles.Count, 0);
            CollectionAssert.AreEquivalent(persist.filesToDelete, new List<string>() { "SAIN.dll" });
        }

        [TestMethod]
        public void TestMismatchedCases()
        {
            string testPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\IntegrationTests", "MismatchedCases"));

            List<string> downloadedFiles = [];

            var persist = RunPlugin(
                testPath,
                syncPaths: ["plugins"],
                configDeleteRemovedFiles: true,
                out List<string> addedFiles,
                out List<string> updatedFiles,
                out List<string> removedFiles,
                ref downloadedFiles
            );

            Assert.AreEqual(addedFiles.Count, 0);
            Assert.AreEqual(updatedFiles.Count, 1);
            Assert.AreEqual(removedFiles.Count, 0);

            Assert.AreEqual(downloadedFiles.Count, 1);
            Assert.AreEqual(downloadedFiles[0], "plugins\\sain.dll");
        }
    }
}
