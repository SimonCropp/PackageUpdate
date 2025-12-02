using System.Xml.Linq;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

public class DirectoryPackagesPropsUpdater
{

    public static async Task UpdateDirectoryPackagesProps(
        string directoryPackagesPropsPath,
        ILogger logger)
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
            .Where(x => x.PackageId != null && x.CurrentVersion != null)
            .ToList();

        // Set up NuGet sources
        var settings = Settings.LoadDefaultSettings(directory);
        var sourceProvider = new PackageSourceProvider(settings);
        var sources = sourceProvider.LoadPackageSources()
            .Where(s => s.IsEnabled)
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
                cache,
                logger);

            if (latestMetadata == null)
            {
                continue;
            }

            var latestVersion = latestMetadata.Identity.Version;

            if (latestVersion > currentVersion)
            {
                // Update the Version attribute
                package.Element.SetAttributeValue("Version", latestVersion.ToString());
                Console.WriteLine($"Updated {package.PackageId}: {currentVersion} -> {latestVersion}");
            }
        }

        // Save the updated file
        doc.Save(directoryPackagesPropsPath);
    }

    static async Task<IPackageSearchMetadata?> GetLatestVersion(
        string packageId,
        NuGetVersion currentVersion,
        List<PackageSource> sources,
        SourceCacheContext cache,
        ILogger logger)
    {
        IPackageSearchMetadata? latestMetadata = null;
        NuGetVersion? latestVersion = null;

        foreach (var source in sources)
        {
            var repository = Repository.Factory.GetCoreV3(source);
            var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>();

            var metadata = await metadataResource.GetMetadataAsync(
                packageId,
                includePrerelease: currentVersion.IsPrerelease,
                includeUnlisted: false,
                cache,
                logger,
                CancellationToken.None);

            var sourceLatest = metadata
                .Where(m => ShouldConsiderVersion(m.Identity.Version, currentVersion))
                .MaxBy(m => m.Identity.Version);

            if (sourceLatest == null ||
                (latestVersion != null && sourceLatest.Identity.Version <= latestVersion))
            {
                continue;
            }

            latestVersion = sourceLatest.Identity.Version;
            latestMetadata = sourceLatest;
        }

        return latestMetadata;
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