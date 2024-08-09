namespace ModSync;

public class ModFile(uint crc, bool nosync = false)
{
    public readonly uint crc = crc;
    public readonly bool nosync = nosync;
}
