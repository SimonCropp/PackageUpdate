public static class PackageSourceReader
{
    static XPlatMachineWideSetting machineSettings = new();

    /// <summary>
    /// The enabled sources from the nuget.config hierarchy of the directory, each with its
    /// <see cref="PackageSource.MinPublishAge"/>.
    /// </summary>
    public static List<PackageSource> Read(string directory) =>
        ReadConfigured(directory).All;

    // NuGet.Configuration reads minPublishAgeHours onto PackageSource.MinPublishAge, and rejects a
    // value that is not a whole number of hours by throwing NuGetConfigurationException. That
    // fails the solution or tool manifest, the same as restore would.
    // https://learn.microsoft.com/nuget/consume-packages/minimum-package-publish-age
    //
    // packageSourceMapping limits which sources a package is looked up in, as restore does, so a
    // version is never taken from a source restore would not use, such as one with no minimum age.
    // https://learn.microsoft.com/nuget/consume-packages/package-source-mapping
    internal static ConfiguredSources ReadConfigured(string directory)
    {
        var settings = Settings.LoadDefaultSettings(
            root: directory,
            configFileName: null,
            machineWideSettings: machineSettings);

        var sources = new PackageSourceProvider(settings)
            .LoadPackageSources()
            .Where(_ => _.IsEnabled)
            .ToList();

        return new(
            sources,
            MinPublishAgeExceptions.GetMinPublishAgeExceptions(settings),
            PackageSourceMapping.GetPackageSourceMapping(settings));
    }

    [Obsolete("Set PackageSource.MinPublishAge, which NuGet.Configuration now reads from nuget.config")]
    public static void SetMinPublishAge(PackageSource source, TimeSpan age) =>
        source.MinPublishAge = age;

    [Obsolete("Use PackageSource.MinPublishAge, which NuGet.Configuration now reads from nuget.config")]
    public static TimeSpan? GetMinPublishAge(PackageSource source)
    {
        if (source.MinPublishAge == TimeSpan.Zero)
        {
            return null;
        }

        return source.MinPublishAge;
    }
}
