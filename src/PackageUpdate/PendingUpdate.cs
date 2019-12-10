class PendingUpdate
{
    public string Package { get; }
    public string Resolved  { get; }
    public string Latest  { get; }
    public bool IsDeprecated { get; }

    public PendingUpdate(string package, string resolved, string latest, bool isDeprecated)
    {
        Package = package;
        Resolved = resolved;
        Latest = latest;
        IsDeprecated = isDeprecated;
    }
}