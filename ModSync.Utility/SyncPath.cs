namespace ModSync;

public class SyncPath(string path, bool enabled = true, bool enforced = false, bool silent = false, bool restartRequired = true)
{
    public readonly string path = path;
    public readonly bool enabled = enabled;
    public readonly bool enforced = enforced;
    public readonly bool silent = silent;
    public readonly bool restartRequired = restartRequired;
}
