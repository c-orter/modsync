using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Aki.Custom.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ModSync
{
    [TestClass]
    public class AddedFilesTests
    {
        [TestMethod]
        public void TestSingleAdded()
        {
            var localModFiles = new Dictionary<string, ModFile>() { { "BepInEx\\plugins\\SAIN\\SAIN.dll", new(1234567, 1000000000000) } };

            var remoteModFiles = new Dictionary<string, ModFile>()
            {
                { "BepInEx\\plugins\\SAIN\\SAIN.dll", new(1234567, 1000000000000) },
                { "BepInEx\\plugins\\Corter-ModSync.dll", new(1234567, 1000000000000) },
            };

            var addedFiles = Sync.GetAddedFiles(localModFiles, remoteModFiles);

            CollectionAssert.AreEqual(addedFiles, new List<string>() { "BepInEx\\plugins\\Corter-ModSync.dll" });
        }

        [TestMethod]
        public void TestOnlyModified()
        {
            var localModFiles = new Dictionary<string, ModFile>() { { "BepInEx\\plugins\\SAIN\\SAIN.dll", new(1234567, 1000000000000) } };

            var remoteModFiles = new Dictionary<string, ModFile>() { { "BepInEx\\plugins\\SAIN\\SAIN.dll", new(2345678, 1000000000001) } };

            var addedFiles = Sync.GetAddedFiles(localModFiles, remoteModFiles);

            Assert.AreEqual(addedFiles.Count, 0);
        }
    }

    [TestClass]
    public class ModifiedFilesTests
    {
        [TestMethod]
        public void TestSingleAdded()
        {
            var localModFiles = new Dictionary<string, ModFile>() { { "BepInEx\\plugins\\SAIN\\SAIN.dll", new(1234567, 1000000000000) } };

            var remoteModFiles = new Dictionary<string, ModFile>()
            {
                { "BepInEx\\plugins\\SAIN\\SAIN.dll", new(1234567, 1000000000000) },
                { "BepInEx\\plugins\\Corter-ModSync.dll", new(1234567, 1000000000000) },
            };

            var previousRemoteModFiles = new Dictionary<string, ModFile>() { { "BepInEx\\plugins\\SAIN\\SAIN.dll", new(1234567, 1000000000000) }, };

            var updatedFiles = Sync.GetUpdatedFiles(localModFiles, remoteModFiles, previousRemoteModFiles);

            Assert.AreEqual(updatedFiles.Count, 0);
        }

        [TestMethod]
        public void TestSingleUpdated()
        {
            var localModFiles = new Dictionary<string, ModFile>()
            {
                { "BepInEx\\plugins\\SAIN\\SAIN.dll", new(1234567, 1000000000000) },
                { "BepInEx\\plugins\\Corter-ModSync.dll", new(1234567, 1000000000000) },
            };

            var remoteModFiles = new Dictionary<string, ModFile>()
            {
                { "BepInEx\\plugins\\SAIN\\SAIN.dll", new(1234567, 1000000000000) },
                { "BepInEx\\plugins\\Corter-ModSync.dll", new(2345678, 1000000000001) },
            };

            var previousRemoteModFiles = new Dictionary<string, ModFile>()
            {
                { "BepInEx\\plugins\\SAIN\\SAIN.dll", new(1234567, 1000000000000) },
                { "BepInEx\\plugins\\Corter-ModSync.dll", new(1234567, 1000000000000) },
            };

            var updatedFiles = Sync.GetUpdatedFiles(localModFiles, remoteModFiles, previousRemoteModFiles);

            CollectionAssert.AreEqual(updatedFiles, new List<string>() { "BepInEx\\plugins\\Corter-ModSync.dll" });
        }

        [TestMethod]
        public void TestOnlyLocalUpdated()
        {
            var localModFiles = new Dictionary<string, ModFile>()
            {
                { "BepInEx\\plugins\\SAIN\\SAIN.dll", new(1234567, 1000000000000) },
                { "BepInEx\\plugins\\Corter-ModSync.dll", new(2345678, 1000000000000) },
            };

            var remoteModFiles = new Dictionary<string, ModFile>()
            {
                { "BepInEx\\plugins\\SAIN\\SAIN.dll", new(1234567, 1000000000000) },
                { "BepInEx\\plugins\\Corter-ModSync.dll", new(1234567, 1000000000000) },
            };

            var previousRemoteModFiles = new Dictionary<string, ModFile>()
            {
                { "BepInEx\\plugins\\SAIN\\SAIN.dll", new(1234567, 1000000000000) },
                { "BepInEx\\plugins\\Corter-ModSync.dll", new(1234567, 1000000000000) },
            };

            var updatedFiles = Sync.GetUpdatedFiles(localModFiles, remoteModFiles, previousRemoteModFiles);

            Assert.AreEqual(updatedFiles.Count, 0);
        }
    }

    [TestClass]
    public class RemovedFilesTests
    {
        [TestMethod]
        public void TestSingleRemoved()
        {
            var localModFiles = new Dictionary<string, ModFile>()
            {
                { "BepInEx\\plugins\\SAIN\\SAIN.dll", new(1234567, 1000000000000) },
                { "BepInEx\\plugins\\Corter-ModSync.dll", new(1234567, 1000000000000) },
            };

            var remoteModFiles = new Dictionary<string, ModFile>() { { "BepInEx\\plugins\\SAIN\\SAIN.dll", new(1234567, 1000000000000) }, };

            var previousRemoteModFiles = new Dictionary<string, ModFile>()
            {
                { "BepInEx\\plugins\\SAIN\\SAIN.dll", new(1234567, 1000000000000) },
                { "BepInEx\\plugins\\Corter-ModSync.dll", new(1234567, 1000000000000) },
            };

            var removedFiles = Sync.GetRemovedFiles(localModFiles, remoteModFiles, previousRemoteModFiles);

            CollectionAssert.AreEqual(removedFiles, new List<string>() { "BepInEx\\plugins\\Corter-ModSync.dll" });
        }
    }

    [TestClass]
    public class HashLocalFilesTests
    {
        readonly Dictionary<string, string> fileContents =
            new()
            {
                { "file1.dll", "Test content" },
                { "file2.dll", "Test content 2" },
                { "file2.dll.nosync", "" },
                { "file3.dll", "Test content 3" },
                { "file3.dll.nosync.txt", "" },
                { "ModName\\mod_name.dll", "Test content 4" },
                { "ModName\\.nosync", "" },
                { "OtherMod\\other_mod.dll", "Test content 5" },
                { "OtherMod\\subdir\\image.png", "Test Image" },
                { "OtherMod\\subdir\\.nosync", "" }
            };

        string testDirectory;

        [TestInitialize]
        public void Setup()
        {
            testDirectory = Utility.GetTemporaryDirectory();

            Directory.CreateDirectory(testDirectory);

            // Create test files
            foreach (var kvp in fileContents)
            {
                var filePath = Path.Combine(testDirectory, kvp.Key);
                var fileParent = Path.GetDirectoryName(filePath);

                if (!Directory.Exists(fileParent))
                    Directory.CreateDirectory(fileParent);

                File.WriteAllText(filePath, kvp.Value);
            }

            Console.WriteLine(testDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(testDirectory, true);
        }

        [TestMethod]
        public void TestHashLocalFiles()
        {
            var expected = fileContents.Where((kvp) => !kvp.Key.EndsWith(".nosync") && !kvp.Key.EndsWith(".nosync.txt")).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var result = Sync.HashLocalFiles(testDirectory, [testDirectory]);

            Assert.IsNotNull(result);
            Assert.AreEqual(result.Count, expected.Count);

            foreach (var kvp in expected)
            {
                Assert.IsTrue(result.ContainsKey(kvp.Key));
                Assert.AreEqual(result[kvp.Key].crc, Crc32.Compute(Encoding.ASCII.GetBytes(kvp.Value)));
            }

            Assert.AreEqual(result.Where(kvp => !kvp.Value.nosync).Count(), 2);
        }

        [TestMethod]
        public void TestHashLocalFilesWithDirectoryThatDoesNotExist()
        {
            var result = Sync.HashLocalFiles(testDirectory, ["bad_directory"]);
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Count, 0);
        }

        [TestMethod]
        public void TestHashLocalFilesWithSingleFile()
        {
            var result = Sync.HashLocalFiles(testDirectory, [Path.Combine(testDirectory, "file1.dll")]);
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Count, 1);
            Assert.IsTrue(result.ContainsKey("file1.dll"));
        }

        [TestMethod]
        public void TestHashLocalFilesWithSingleFileThatDoesNotExist()
        {
            var result = Sync.HashLocalFiles(testDirectory, [Path.Combine(testDirectory, "does_not_exist.dll")]);
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Count, 0);
        }
    }
}
