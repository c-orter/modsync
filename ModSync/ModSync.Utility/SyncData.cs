using System.Collections.Generic;

namespace ModSync
{
    public class ModFile(uint crc, long modified, bool nosync = false)
    {
        public uint crc = crc;
        public long modified = modified;
        public bool nosync = nosync;
    }

    public class Persist
    {
        public Dictionary<string, ModFile> previousSync = [];
        public string downloadDir = string.Empty;
        public List<string> filesToDelete = [];
    }
}
