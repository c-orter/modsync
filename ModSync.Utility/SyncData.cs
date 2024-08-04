using System;
using System.Collections.Generic;

namespace ModSync
{
    public class SyncPath(string path, bool enabled = true, bool enforced = false, bool silent = false, bool restartRequired = true)
    {
        public readonly string path = path;
        public readonly bool enabled = enabled;
        public readonly bool enforced = enforced;
        public readonly bool silent = silent;
        public readonly bool restartRequired = restartRequired;
    }

    public class ModFile(uint crc, bool nosync = false)
    {
        public readonly uint crc = crc;
        public readonly bool nosync = nosync;
    }

    public class Persist
    {
        public const int LATEST_VERSION = 7;

        public Dictionary<string, Dictionary<string, ModFile>> previousSync = [];
        public string downloadDir = string.Empty;
        public List<string> filesToDelete = [];
        public int version = 0;
    }
}
