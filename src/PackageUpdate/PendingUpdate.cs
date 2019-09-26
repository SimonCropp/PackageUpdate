class PendingUpdate
{
    public string Package { get; }
    public string Version { get; }

    public PendingUpdate(string package, string version)
    {
        Package = package;
        Version = version;
    }
}