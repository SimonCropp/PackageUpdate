public static class PackageSourceReader
{
    static XPlatMachineWideSetting machineSettings = new();

    public static List<PackageSource> Read(string directory)
    {
        // Set up NuGet sources
        var settings = Settings.LoadDefaultSettings(
            root: directory,
            configFileName: null,
            machineWideSettings: machineSettings);

        var provider = new PackageSourceProvider(settings);
        return provider.LoadPackageSources()
            .Where(_ => _.IsEnabled)
            .ToList();
    }
}