using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SPT.Custom.Utils;

namespace ModSync.Tests
{
    [TestClass]
    public class AddedFilesTests
    {
        [TestMethod]
        public void TestSingleAdded()
        {
            var localModFiles = new Dictionary<string, ModFile>() { { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) } };

            var remoteModFiles = new Dictionary<string, ModFile>()
            {
                { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) },
                { @"BepInEx\plugins\Corter-ModSync.dll", new(1234567) },
            };

            var addedFiles = Sync.GetAddedFiles(localModFiles, remoteModFiles);

            CollectionAssert.AreEqual(new List<string>() { @"BepInEx\plugins\Corter-ModSync.dll" }, addedFiles);
        }

        [TestMethod]
        public void TestSingleAddedNoSync()
        {
            var localModFiles = new Dictionary<string, ModFile>() { { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) } };

            var remoteModFiles = new Dictionary<string, ModFile>()
            {
                { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) },
                { @"BepInEx\plugins\Corter-ModSync.dll", new(1234567) },
            };

            var addedFiles = Sync.GetAddedFiles(localModFiles, remoteModFiles);

            CollectionAssert.AreEqual(new List<string>() { @"BepInEx\plugins\Corter-ModSync.dll" }, addedFiles);
        }

        [TestMethod]
        public void TestNoneAdded()
        {
            var localModFiles = new Dictionary<string, ModFile>() { { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) } };

            var remoteModFiles = new Dictionary<string, ModFile>() { { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) }, };

            var addedFiles = Sync.GetAddedFiles(localModFiles, remoteModFiles);

            Assert.AreEqual(0, addedFiles.Count);
        }
    }

    [TestClass]
    public class UpdatedFilesTests
    {
        [TestMethod]
        public void TestSingleAdded()
        {
            var localModFiles = new Dictionary<string, ModFile>() { { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) } };

            var remoteModFiles = new Dictionary<string, ModFile>()
            {
                { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) },
                { @"BepInEx\plugins\Corter-ModSync.dll", new(1234567) },
            };

            var previousRemoteModFiles = new Dictionary<string, ModFile>() { { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) }, };

            var updatedFiles = Sync.GetUpdatedFiles(localModFiles, remoteModFiles, previousRemoteModFiles);

            Assert.AreEqual(0, updatedFiles.Count);
        }

        [TestMethod]
        public void TestSingleUpdated()
        {
            var localModFiles = new Dictionary<string, ModFile>()
            {
                { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) },
                { @"BepInEx\plugins\Corter-ModSync.dll", new(1234567) },
            };

            var remoteModFiles = new Dictionary<string, ModFile>()
            {
                { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) },
                { @"BepInEx\plugins\Corter-ModSync.dll", new(2345678) },
            };

            var previousRemoteModFiles = new Dictionary<string, ModFile>()
            {
                { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) },
                { @"BepInEx\plugins\Corter-ModSync.dll", new(1234567) },
            };

            var updatedFiles = Sync.GetUpdatedFiles(localModFiles, remoteModFiles, previousRemoteModFiles);

            CollectionAssert.AreEqual(new List<string>() { @"BepInEx\plugins\Corter-ModSync.dll" }, updatedFiles);
        }

        [TestMethod]
        public void TestOnlyLocalUpdated()
        {
            var localModFiles = new Dictionary<string, ModFile>()
            {
                { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) },
                { @"BepInEx\plugins\Corter-ModSync.dll", new(2345678) },
            };

            var remoteModFiles = new Dictionary<string, ModFile>()
            {
                { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) },
                { @"BepInEx\plugins\Corter-ModSync.dll", new(1234567) },
            };

            var previousRemoteModFiles = new Dictionary<string, ModFile>()
            {
                { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) },
                { @"BepInEx\plugins\Corter-ModSync.dll", new(1234567) },
            };

            var updatedFiles = Sync.GetUpdatedFiles(localModFiles, remoteModFiles, previousRemoteModFiles);

            Assert.AreEqual(0, updatedFiles.Count);
        }

        [TestMethod]
        public void TestFilesExistButPreviousEmpty()
        {
            var localModFiles = new Dictionary<string, ModFile>()
            {
                { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) },
                { @"BepInEx\plugins\Corter-ModSync.dll", new(1234567) },
            };

            var remoteModFiles = new Dictionary<string, ModFile>()
            {
                { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) },
                { @"BepInEx\plugins\Corter-ModSync.dll", new(2345678) },
                { @"BepInEx\plugins\New-Mod.dll", new(1234567) },
            };

            var previousRemoteModFiles = new Dictionary<string, ModFile>();

            var updatedFiles = Sync.GetUpdatedFiles(localModFiles, remoteModFiles, previousRemoteModFiles);

            Assert.AreEqual(1, updatedFiles.Count);
            Assert.AreEqual(@"BepInEx\plugins\Corter-ModSync.dll", updatedFiles[0]);
        }

        [TestMethod]
        public void TestBothUpdated()
        {
            var localModFiles = new Dictionary<string, ModFile>()
            {
                { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) },
                { @"BepInEx\plugins\Corter-ModSync.dll", new(2345678) },
            };

            var remoteModFiles = new Dictionary<string, ModFile>()
            {
                { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) },
                { @"BepInEx\plugins\Corter-ModSync.dll", new(2345678) },
            };

            var previousRemoteModFiles = new Dictionary<string, ModFile>()
            {
                { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) },
                { @"BepInEx\plugins\Corter-ModSync.dll", new(1234567) },
            };
            
            var updatedFiles = Sync.GetUpdatedFiles(localModFiles, remoteModFiles, previousRemoteModFiles);

            Assert.AreEqual(0, updatedFiles.Count);
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
                { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) },
                { @"BepInEx\plugins\Corter-ModSync.dll", new(1234567) },
            };

            var remoteModFiles = new Dictionary<string, ModFile>() { { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) }, };

            var previousRemoteModFiles = new Dictionary<string, ModFile>()
            {
                { @"BepInEx\plugins\SAIN\SAIN.dll", new(1234567) },
                { @"BepInEx\plugins\Corter-ModSync.dll", new(1234567) },
            };

            var removedFiles = Sync.GetRemovedFiles(localModFiles, remoteModFiles, previousRemoteModFiles);

            CollectionAssert.AreEqual(new List<string>() { @"BepInEx\plugins\Corter-ModSync.dll" }, removedFiles);
        }
    }

    [TestClass]
    public class HashLocalFilesTests
    {
        readonly Dictionary<string, string> fileContents =
            new()
            {
                { @"plugins\file1.dll", "Test content" },
                { @"plugins\file2.dll", "Test content 2" },
                { @"plugins\file2.dll.nosync", "" },
                { @"plugins\file3.dll", "Test content 3" },
                { @"plugins\file3.dll.nosync.txt", "" },
                { @"plugins\ModName\mod_name.dll", "Test content 4" },
                { @"plugins\ModName\.nosync", "" },
                { @"plugins\OtherMod\other_mod.dll", "Test content 5" },
                { @"plugins\OtherMod\subdir\image.png", "Test Image" },
                { @"plugins\OtherMod\subdir\.nosync", "" }
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

                if (fileParent != null && !Directory.Exists(fileParent))
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
            var expected = fileContents
                .Where((kvp) => !kvp.Key.EndsWith(".nosync") && !kvp.Key.EndsWith(".nosync.txt"))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var result = Sync.HashLocalFiles(testDirectory, ["plugins"], ["plugins"]);

            Assert.IsNotNull(result);
            Assert.AreEqual(expected.Count, result.Count);

            foreach (var kvp in expected)
            {
                Assert.IsTrue(result.ContainsKey(kvp.Key));
                Assert.AreEqual(Crc32.Compute(Encoding.ASCII.GetBytes(kvp.Value)), result[kvp.Key].crc);
            }

            Assert.AreEqual(2, result.Count(kvp => !kvp.Value.nosync));
        }

        [TestMethod]
        public void TestHashLocalFilesWithDirectoryThatDoesNotExist()
        {
            var result = Sync.HashLocalFiles(testDirectory, ["bad_directory"], ["bad_directory"]);
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void TestHashLocalFilesWithSingleFile()
        {
            var result = Sync.HashLocalFiles(
                testDirectory,
                [Path.Combine(testDirectory, "plugins\\file1.dll")],
                [Path.Combine(testDirectory, "plugins\\file1.dll")]
            );
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.ContainsKey("plugins\\file1.dll"));
        }

        [TestMethod]
        public void TestHashLocalFilesWithSingleFileThatDoesNotExist()
        {
            var result = Sync.HashLocalFiles(
                testDirectory,
                [Path.Combine(testDirectory, "does_not_exist.dll")],
                [Path.Combine(testDirectory, "does_not_exist.dll")]
            );
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void TestIncludeDisabledWithNoSync()
        {
            var result = Sync.HashLocalFiles(testDirectory, [Path.Combine(testDirectory, "plugins\\file1.dll")], []);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.ContainsKey("plugins\\file1.dll"));
            Assert.IsTrue(result["plugins\\file1.dll"].nosync);
        }
    }

    [TestClass]
    public class CreateModFileTest
    {
        readonly Dictionary<string, string> fileContents =
            new()
            {
                { "file1.dll", "" },
                { "file2.dll", "" },
                { "file2.dll.nosync", "" },
                { "file3.dll", "Test content 3" },
                { "file3.dll.nosync.txt", "" },
                { @"ModName\mod_name.dll", "Test content 4" },
                { @"ModName\.nosync", "" },
                { @"OtherMod\other_mod.dll", "Test content 5" },
                { @"OtherMod\subdir\image.png", "Test Image" },
                { @"OtherMod\subdir\.nosync", "" }
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
        public void TestCreateModFile()
        {
            var kv = Sync.CreateModFile(testDirectory, Path.Combine(testDirectory, "file1.dll"), true);

            Assert.IsNotNull(kv.Value);
            Assert.AreEqual(0u, kv.Value.crc);
            Assert.IsFalse(kv.Value.nosync);
        }

        [TestMethod]
        public void TestCreateModFileDisabled()
        {
            var kv = Sync.CreateModFile(testDirectory, Path.Combine(testDirectory, "file1.dll"), false);

            Assert.IsNotNull(kv.Value);
            Assert.AreEqual(0u, kv.Value.crc);
            Assert.IsTrue(kv.Value.nosync);
        }

        [TestMethod]
        public void TestCreateModFileWithNoSync()
        {
            var kv = Sync.CreateModFile(testDirectory, Path.Combine(testDirectory, "file2.dll"), true);

            Assert.IsNotNull(kv.Value);
            Assert.AreEqual(0u, kv.Value.crc);
            Assert.IsTrue(kv.Value.nosync);
        }
    }
}
