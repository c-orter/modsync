using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ModSync
{
    [TestClass]
    public class NoSyncInTreeTest
    {
        private readonly Dictionary<string, string> fileContents =
            new()
            {
                { "file1.dll", "Test content" },
                { "file2.dll", "Test content 2" },
                { "file2.dll.nosync", "" },
                { "file3.dll", "Test content 3" },
                { "file3.dll.nosync.txt", "" },
                { @"ModName\mod_name.dll", "Test content 4" },
                { @"ModName\.nosync", "" },
                { @"ModName\subdir\image.png", "Test Image 1" },
                { @"OtherMod\other_mod.dll", "Test content 5" },
                { @"OtherMod\subdir\image.png", "Test Image 2" },
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
        public void TestNoSyncInTreeWithoutNoSync()
        {
            var result = Utility.NoSyncInTree(testDirectory, "file1.dll");
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TestNoSyncInTreeWithNoSync()
        {
            var result = Utility.NoSyncInTree(testDirectory, "file2.dll");
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TestNoSyncInTreeWithNoSyncTxt()
        {
            var result = Utility.NoSyncInTree(testDirectory, "file3.dll");
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TestNoSyncInTreeWithNoSyncInParent()
        {
            var result = Utility.NoSyncInTree(testDirectory, @"ModName\subdir\image.png");
            Assert.IsTrue(result);
        }
    }
}
