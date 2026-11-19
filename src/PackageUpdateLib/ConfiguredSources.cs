/// <summary>
/// The sources configured for a directory, which of them each package may come from under its
/// nuget.config's packageSourceMapping, and the packages exempt from their minPublishAgeHours via
/// minPublishAgeExceptions.
/// </summary>
class ConfiguredSources(List<PackageSource> sources, MinPublishAgeExceptions exceptions, PackageSourceMapping mapping)
{
    // The same sources with no minimum publish age, for the exempt packages. Cloned, since the
    // sources themselves are what Read returns
    List<PackageSource> withoutMinPublishAge = sources
        .Select(WithoutMinPublishAge)
        .ToList();

    public List<PackageSource> All => sources;

    /// <summary>
    /// The sources to look the package up in: only those packageSourceMapping maps it to, when mapping
    /// is enabled, and without a minimum publish age when the package is exempt.
    /// </summary>
    public List<PackageSource> For(string package)
    {
        var candidates = exceptions.FindException(package) == null ? sources : withoutMinPublishAge;

        if (!mapping.IsEnabled)
        {
            return candidates;
        }

        // Restore matches the mapped source names case-insensitively
        var mapped = mapping.GetConfiguredPackageSources(package);
        return candidates
            .Where(_ => mapped.Contains(_.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// True when packageSourceMapping is enabled and maps the package to none of the enabled sources,
    /// so restore cannot find it either.
    /// </summary>
    public bool IsUnmapped(string package) =>
        mapping.IsEnabled &&
        For(package).Count == 0;

    static PackageSource WithoutMinPublishAge(PackageSource source)
    {
        var clone = source.Clone();
        clone.MinPublishAge = TimeSpan.Zero;
        return clone;
    }
}
