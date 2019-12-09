class PendingUpdate
{
    public string Package { get; }
    public string Resolved  { get; }
    public string Latests  { get; }

    public PendingUpdate(string package, string resolved, string latests)
    {
        Package = package;
        Resolved = resolved;
        Latests = latests;
    }
}