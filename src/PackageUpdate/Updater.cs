using NuGet.Packaging.Core;

public static class Updater
{
    public static async Task Update(
        string directoryPackagesPropsPath,
        string? packageName = null)
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
                CurrentVersion = element.Attribute("Version")?.Value
            })
            .Where(_ => _.PackageId != null &&
                        _.CurrentVersion != null)
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
        var settings = Settings.LoadDefaultSettings(directory);
        var sourceProvider = new PackageSourceProvider(settings);
        var sources = sourceProvider.LoadPackageSources()
            .Where(_ => _.IsEnabled)
            .ToList();

        var cache = new SourceCacheContext();

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

        // Save the updated file
        doc.Save(directoryPackagesPropsPath);
    }
    public static async Task<IPackageSearchMetadata?> GetLatestVersion(
        string packageId,
        NuGetVersion currentVersion,
        List<PackageSource> sources,
        SourceCacheContext cache)
    {
        NuGetVersion? latestVersion = null;

        foreach (var source in sources)
        {
            var repository = Repository.Factory.GetCoreV3(source);

            // Use FindPackageByIdResource to efficiently get version list
            var findResource = await repository.GetResourceAsync<FindPackageByIdResource>();

            var versions = await findResource.GetAllVersionsAsync(
                packageId,
                cache,
                SerilogNuGetLogger.Instance,
                Cancel.None);

            var sourceLatest = versions
                .Where(v => ShouldConsiderVersion(v, currentVersion))
                .MaxBy(v => v);

            if (sourceLatest == null ||
                (latestVersion != null && sourceLatest <= latestVersion))
            {
                continue;
            }

            latestVersion = sourceLatest;
        }

        if (latestVersion == null)
        {
            return null;
        }

        // Only get metadata for the specific latest version we found
        foreach (var source in sources)
        {
            var repository = Repository.Factory.GetCoreV3(source);
            var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>();

            var metadata = await metadataResource.GetMetadataAsync(
                new PackageIdentity(packageId, latestVersion),
                cache,
                SerilogNuGetLogger.Instance,
                Cancel.None);

            if (metadata != null)
            {
                return metadata;
            }
        }

        return null;
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