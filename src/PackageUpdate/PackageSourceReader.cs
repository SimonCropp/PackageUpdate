public static class PackageSourceReader
{
    static XPlatMachineWideSetting machineSettings = new();
    static ConditionalWeakTable<PackageSource, StrongBox<TimeSpan>> minPublishAges = new();

    public static List<PackageSource> Read(string directory)
    {
        // Set up NuGet sources
        var settings = Settings.LoadDefaultSettings(
            root: directory,
            configFileName: null,
            machineWideSettings: machineSettings);

        var provider = new PackageSourceProvider(settings);
        var sources = provider.LoadPackageSources()
            .Where(_ => _.IsEnabled)
            .ToList();

        var ages = ReadMinPublishAges(settings);
        foreach (var source in sources)
        {
            if (ages.TryGetValue(source.Name, out var age))
            {
                minPublishAges.AddOrUpdate(source, new(age));
            }
        }

        return sources;
    }

    // Reads the minPublishAgeHours attribute from packageSources entries.
    // https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/rc2/nuget.md#configure-minimum-package-publication-age-from-the-cli
    // NuGet.Configuration drops unknown attributes from SourceItem, so the config files are read directly.
    // GetConfigFilePaths is ordered closest first, so apply in reverse so closer files win.
    static Dictionary<string, TimeSpan> ReadMinPublishAges(ISettings settings)
    {
        var ages = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in settings.GetConfigFilePaths().Reverse())
        {
            XElement? section;
            try
            {
                section = XDocument.Load(path).Root?.Element("packageSources");
            }
            catch (Exception exception) when (exception is IOException or XmlException or UnauthorizedAccessException)
            {
                continue;
            }

            if (section == null)
            {
                continue;
            }

            foreach (var element in section.Elements())
            {
                if (element.Name.LocalName == "clear")
                {
                    ages.Clear();
                    continue;
                }

                var key = element.Attribute("key")?.Value;
                if (element.Name.LocalName != "add" || key == null)
                {
                    continue;
                }

                var value = element.Attribute("minPublishAgeHours")?.Value;
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var hours) &&
                    hours > 0)
                {
                    ages[key] = TimeSpan.FromHours(hours);
                    continue;
                }

                ages.Remove(key);
            }
        }

        return ages;
    }

    public static void SetMinPublishAge(PackageSource source, TimeSpan age) =>
        minPublishAges.AddOrUpdate(source, new(age));

    public static TimeSpan? GetMinPublishAge(PackageSource source)
    {
        if (minPublishAges.TryGetValue(source, out var age))
        {
            return age.Value;
        }

        return null;
    }
}
