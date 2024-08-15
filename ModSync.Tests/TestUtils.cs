using System.IO;

namespace ModSync.Tests;

public static class TestUtils
{
    public static string GetTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        if (File.Exists(tempDirectory))
            return GetTemporaryDirectory();

        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }
}
