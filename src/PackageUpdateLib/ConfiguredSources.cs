/// <summary>
/// The sources configured for a directory, and the packages its nuget.config exempts from their
/// minPublishAgeHours via minPublishAgeExceptions.
/// </summary>
class ConfiguredSources(List<PackageSource> sources, MinPublishAgeExceptions exceptions)
{
    // The same sources with no minimum publish age, for the exempt packages. Cloned, since the
    // sources themselves are what Read returns
    List<PackageSource> withoutMinPublishAge = sources
        .Select(WithoutMinPublishAge)
        .ToList();

    public List<PackageSource> All => sources;

    /// <summary>
    /// The sources to look the package up in: without a minimum publish age when the package is exempt.
    /// </summary>
    public List<PackageSource> For(string package)
    {
        if (exceptions.FindException(package) == null)
        {
            return sources;
        }

        return withoutMinPublishAge;
    }

    static PackageSource WithoutMinPublishAge(PackageSource source)
    {
        var clone = source.Clone();
        clone.MinPublishAge = TimeSpan.Zero;
        return clone;
    }
}
