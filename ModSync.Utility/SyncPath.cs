namespace ModSync;

public class SyncPath(string path, bool enabled = true, bool enforced = false, bool silent = false)
{
    public readonly string path = path;
    public readonly bool enabled = enabled;
    public readonly bool enforced = enforced;
    public readonly bool silent = silent;
}
