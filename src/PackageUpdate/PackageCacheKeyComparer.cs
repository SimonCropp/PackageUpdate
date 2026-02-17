sealed class PackageCacheKeyComparer :
    IEqualityComparer<(string Package, NuGetVersion Version)>
{
    public static PackageCacheKeyComparer Instance { get; } = new();

    public bool Equals((string Package, NuGetVersion Version) x, (string Package, NuGetVersion Version) y) =>
        string.Equals(x.Package, y.Package, StringComparison.OrdinalIgnoreCase) &&
        EqualityComparer<NuGetVersion>.Default.Equals(x.Version, y.Version);

    public int GetHashCode((string Package, NuGetVersion Version) obj) =>
        HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Package),
            EqualityComparer<NuGetVersion>.Default.GetHashCode(obj.Version));
}
