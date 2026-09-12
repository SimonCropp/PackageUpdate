internal sealed class TempDir :
    IDisposable
{
    public string Path { get; }

    public TempDir()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "PackageUpdateTests",
            Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path);
    }

    public void Dispose() =>
        Directory.Delete(Path, true);
}
