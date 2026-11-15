/// <summary>
/// What <see cref="Updater"/> has looked up, kept for as long as the <see cref="SourceCacheContext"/> it
/// was looked up with, so each <see cref="PackageUpdater"/> run sees versions published since the last.
/// Kept apart per set of sources, since solutions under one directory can each have their own
/// nuget.config, with different feeds or minPublishAgeHours.
/// </summary>
class LookupCache
{
    static ConditionalWeakTable<SourceCacheContext, ConcurrentDictionary<string, LookupCache>> caches = new();

    /// <summary>The metadata of a version, from the first source that has it.</summary>
    public ConcurrentDictionary<(string Package, NuGetVersion Version), IPackageSearchMetadata?> Metadata { get; } = new(PackageCacheKeyComparer.Instance);

    /// <summary>The latest version to move to from a current version.</summary>
    public ConcurrentDictionary<(string Package, NuGetVersion CurrentVersion), IPackageSearchMetadata?> Latest { get; } = new(PackageCacheKeyComparer.Instance);

    public static LookupCache For(SourceCacheContext context, List<PackageSource> sources)
    {
        var bySources = caches.GetValue(context, _ => new(StringComparer.Ordinal));
        return bySources.GetOrAdd(Key(sources), _ => new());
    }

    // The source urls in order, each with its minPublishAgeHours, since both decide what is found
    static string Key(List<PackageSource> sources) =>
        string.Join(
            '\n',
            sources.Select(_ => $"{_.Source} {PackageSourceReader.GetMinPublishAge(_)}"));
}
