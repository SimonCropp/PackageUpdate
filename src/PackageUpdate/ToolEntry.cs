/// <summary>
/// A tool declared in a `dotnet-tools.json` manifest.
/// <see cref="VersionStart" /> and <see cref="VersionLength" /> locate the version value in the
/// manifest bytes, so it can be replaced without reformatting the rest of the file.
/// </summary>
record ToolEntry(
    string Package,
    string Version,
    int VersionStart,
    int VersionLength,
    bool Pinned);
