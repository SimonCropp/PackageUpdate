public static class Updater
{
    public static async Task Update(
        SourceCacheContext cache,
        string directoryPackagesPropsPath,
        string? packageName)
    {
        var directory = Path.GetDirectoryName(directoryPackagesPropsPath)!;

        // Load the XML document
        var xml = XDocument.Load(directoryPackagesPropsPath);

        // Read current package versions
        var packageVersions = xml.Descendants("PackageVersion")
            .Select(element => new
            {
                Element = element,
                Package = element.Attribute("Include")?.Value,
                CurrentVersion = element.Attribute("Version")?.Value,
                Pinned = element.Attribute("Pinned")?.Value == "true"
            })
            .Where(_ => _.Package != null &&
                        _.CurrentVersion != null &&
                        !_.Pinned)
            .ToList();

        // Filter to specific package if requested
        if (!string.IsNullOrEmpty(packageName))
        {
            packageVersions = packageVersions
                .Where(_ => string.Equals(_.Package, packageName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (packageVersions.Count == 0)
            {
                Log.Warning("Package {Package} not found in {FilePath}", packageName, directoryPackagesPropsPath);
                return;
            }
        }

        var sources = PackageSourceReader.Read(directory);

        // Update each package
        foreach (var package in packageVersions)
        {
            if (!NuGetVersion.TryParse(package.CurrentVersion, out var currentVersion))
            {
                continue;
            }

            var latestMetadata = await GetLatestVersion(
                package.Package!,
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
            Log.Information("Updated {Package}: {NuGetVersion} -> {LatestVersion}", package.Package, currentVersion, latestVersion);
        }

        await using var writer = XmlWriter.Create(directoryPackagesPropsPath, xmlSettings);
        await xml.SaveAsync(writer, Cancel.None);
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
            var (repository, metadataResource) = await RepositoryReader.Read(source);

            var condidates = await GetCondidates(package, currentVersion, cache, repository);

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

    static async Task<List<NuGetVersion>> GetCondidates(string package, NuGetVersion currentVersion, SourceCacheContext cache, SourceRepository repository)
    {
        // Use FindPackageByIdResource to efficiently get version list
        var findResource = await repository.GetResourceAsync<FindPackageByIdResource>();

        var versions = await findResource.GetAllVersionsAsync(
            package,
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