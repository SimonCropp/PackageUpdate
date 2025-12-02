public static class Updater
{
    public static async Task Update(
        string directoryPackagesPropsPath,
        string? packageName)
    {
        var directory = Path.GetDirectoryName(directoryPackagesPropsPath)!;

        // Load the XML document
        var doc = XDocument.Load(directoryPackagesPropsPath);

        // Read current package versions
        var packageVersions = doc.Descendants("PackageVersion")
            .Select(element => new
            {
                Element = element,
                PackageId = element.Attribute("Include")?.Value,
                CurrentVersion = element.Attribute("Version")?.Value,
                ShouldUpdate = element.Attribute("Update")?.Value != "false"
            })
            .Where(_ => _.PackageId != null &&
                        _.CurrentVersion != null &&
                        _.ShouldUpdate)
            .ToList();

        // Filter to specific package if requested
        if (!string.IsNullOrEmpty(packageName))
        {
            packageVersions = packageVersions
                .Where(_ => string.Equals(_.PackageId, packageName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (packageVersions.Count == 0)
            {
                Log.Warning("Package {PackageName} not found in {FilePath}", packageName, directoryPackagesPropsPath);
                return;
            }
        }

        // Set up NuGet sources
        var settings = Settings.LoadDefaultSettings(
            root: directory,
            configFileName: null,
            machineWideSettings: new XPlatMachineWideSetting());

        var sourceProvider = new PackageSourceProvider(settings);
        var sources = sourceProvider.LoadPackageSources()
            .Where(_ => _.IsEnabled)
            .ToList();

        using var cache = new SourceCacheContext();

        // Update each package
        foreach (var package in packageVersions)
        {
            if (!NuGetVersion.TryParse(package.CurrentVersion, out var currentVersion))
            {
                continue;
            }

            var latestMetadata = await GetLatestVersion(
                package.PackageId!,
                currentVersion,
                sources,
                cache);

            if (latestMetadata == null)
            {
                continue;
            }

            var latestVersion = latestMetadata.Identity.Version;

            if (latestVersion <= currentVersion)
            {
                continue;
            }

            // Update the Version attribute
            package.Element.SetAttributeValue("Version", latestVersion.ToString());
            Log.Information("Updated {PackageId}: {NuGetVersion} -> {LatestVersion}", package.PackageId, currentVersion, latestVersion);
        }

        await using var writer = XmlWriter.Create(directoryPackagesPropsPath, xmlSettings);
        await doc.SaveAsync(writer, Cancel.None);
    }

    static XmlWriterSettings xmlSettings = new()
    {
        OmitXmlDeclaration = true,
        Indent = true,
        IndentChars = "  ",
        Async = true
    };

    public static async Task<IPackageSearchMetadata?> GetLatestVersion(
        string package,
        NuGetVersion currentVersion,
        List<PackageSource> sources,
        SourceCacheContext cache)
    {
        IPackageSearchMetadata? latestMetadata = null;

        foreach (var source in sources)
        {
            var repository = Repository.Factory.GetCoreV3(source);

            var condidates = await GetCondidates(package, currentVersion, cache, repository);

            // Check each candidate version to see if it's listed
            var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>();

            foreach (var candidate in condidates)
            {
                var metadata = await metadataResource.GetMetadataAsync(
                    new(package, candidate),
                    cache,
                    SerilogNuGetLogger.Instance,
                    Cancel.None);

                // Skip unlisted packages
                if (metadata is not {IsListed: true})
                {
                    continue;
                }

                // Found a listed version - check if it's better than what we have
                if (latestMetadata == null ||
                    candidate > latestMetadata.Identity.Version)
                {
                    // Found the best version from this source
                    latestMetadata = metadata;
                    break;
                }
            }
        }

        return latestMetadata;
    }

    static async Task<List<NuGetVersion>> GetCondidates(string packageId, NuGetVersion currentVersion, SourceCacheContext cache, SourceRepository repository)
    {
        // Use FindPackageByIdResource to efficiently get version list
        var findResource = await repository.GetResourceAsync<FindPackageByIdResource>();

        var versions = await findResource.GetAllVersionsAsync(
            packageId,
            cache,
            SerilogNuGetLogger.Instance,
            Cancel.None);

        return versions
            .Where(v => ShouldConsiderVersion(v, currentVersion))
            .OrderByDescending(_ => _)
            .ToList();
    }

    static bool ShouldConsiderVersion(NuGetVersion candidate, NuGetVersion current)
    {
        // If current is stable, only consider stable or newer versions
        // If current is pre-release, consider any newer version
        if (!current.IsPrerelease && candidate.IsPrerelease)
        {
            return false;
        }

        return candidate > current;
    }
}