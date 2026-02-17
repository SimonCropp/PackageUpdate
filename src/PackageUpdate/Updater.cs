public static class Updater
{
    static ConcurrentDictionary<(string Package, NuGetVersion Version), IPackageSearchMetadata?> metadataCache = new(PackageCacheKeyComparer.Instance);
    static ConcurrentDictionary<(string Package, NuGetVersion CurrentVersion), IPackageSearchMetadata?> latestVersionCache = new(PackageCacheKeyComparer.Instance);

    public static async Task Update(
        SourceCacheContext cache,
        string directoryPackagesPropsPath,
        string? packageName)
    {
        var directory = Path.GetDirectoryName(directoryPackagesPropsPath)!;

        // Detect the original newline style and trailing newline
        var (newLine, hasTrailingNewline) = DetectNewLineInfo(directoryPackagesPropsPath);

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

        // Track migrations for csproj updates
        var migrations = new List<(string OldPackage, string NewPackage)>();

        // Update each package
        foreach (var package in packageVersions)
        {
            if (!NuGetVersion.TryParse(package.CurrentVersion, out var currentVersion))
            {
                continue;
            }

            // Check if current version is deprecated and attempt migration
            var currentMetadata = await GetPackageMetadata(
                package.Package!,
                currentVersion,
                sources,
                cache);

            if (currentMetadata != null)
            {
                var deprecation = await currentMetadata.GetDeprecationMetadataAsync();
                if (deprecation != null)
                {
                    var migration = await TryMigratePackage(
                        package.Element,
                        package.Package!,
                        deprecation,
                        sources,
                        cache,
                        xml);

                    if (migration != null)
                    {
                        migrations.Add(migration.Value);
                        // Migration successful, skip normal version update
                        continue;
                    }
                    // If migration failed, continue with normal version update
                }
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

        var xmlSettings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            IndentChars = "  ",
            NewLineChars = newLine,
            Async = true
        };

        await using (var writer = XmlWriter.Create(directoryPackagesPropsPath, xmlSettings))
        {
            await xml.SaveAsync(writer, Cancel.None);
        }

        // Match the original trailing newline convention
        if (hasTrailingNewline)
        {
            await File.AppendAllTextAsync(directoryPackagesPropsPath, newLine);
        }

        // Update PackageReference entries in csproj files for migrated packages
        if (migrations.Count > 0)
        {
            await UpdateCsprojFiles(directory, migrations);
        }
    }

    static (string newLine, bool hasTrailingNewline) DetectNewLineInfo(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var newLine = Environment.NewLine;
        var hasTrailingNewline = false;

        // Detect newline style from first occurrence
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == '\r')
            {
                if (i + 1 < bytes.Length && bytes[i + 1] == '\n')
                {
                    newLine = "\r\n";
                }
                else
                {
                    newLine = "\r";
                }
                break;
            }
            if (bytes[i] == '\n')
            {
                newLine = "\n";
                break;
            }
        }

        // Detect trailing newline
        if (bytes.Length > 0)
        {
            var lastByte = bytes[^1];
            hasTrailingNewline = lastByte == '\n' || lastByte == '\r';
        }

        return (newLine, hasTrailingNewline);
    }

    public static async Task<IPackageSearchMetadata?> GetLatestVersion(
        string package,
        NuGetVersion currentVersion,
        List<PackageSource> sources,
        SourceCacheContext cache)
    {
        var key = (package, currentVersion);
        if (latestVersionCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

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

        latestVersionCache[key] = latestMetadata;
        return latestMetadata;
    }

    static async Task<IPackageSearchMetadata?> GetPackageMetadata(
        string package,
        NuGetVersion version,
        List<PackageSource> sources,
        SourceCacheContext cache)
    {
        var key = (package, version);
        if (metadataCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        foreach (var source in sources)
        {
            var (_, metadataResource) = await RepositoryReader.Read(source);

            var metadata = await metadataResource.GetMetadataAsync(
                new(package, version),
                cache,
                SerilogNuGetLogger.Instance,
                Cancel.None);

            if (metadata != null)
            {
                metadataCache[key] = metadata;
                return metadata;
            }
        }

        metadataCache[key] = null;
        return null;
    }

    static async Task<(string OldPackage, string NewPackage)?> TryMigratePackage(
        XElement packageElement,
        string currentPackage,
        PackageDeprecationMetadata deprecation,
        List<PackageSource> sources,
        SourceCacheContext cache,
        XDocument xml)
    {
        // Check if alternate package exists
        var alternatePackage = deprecation.AlternatePackage;
        if (alternatePackage == null)
        {
            Log.Warning(
                "Package {Package} is deprecated but has no alternative. Reasons: {Reasons}",
                currentPackage,
                string.Join(", ", deprecation.Reasons));
            return null;
        }

        // Check if alternate already exists in Directory.Packages.props
        var existingAlternate = xml.Descendants("PackageVersion")
            .FirstOrDefault(_ =>
                string.Equals(
                    _.Attribute("Include")?.Value,
                    alternatePackage.PackageId,
                    StringComparison.OrdinalIgnoreCase));

        if (existingAlternate != null)
        {
            Log.Warning(
                "Package {Package} is deprecated with alternative {Alternative}, but alternative already exists",
                currentPackage,
                alternatePackage.PackageId);
            return null;
        }

        // Verify alternate package exists in NuGet sources
        var alternateMetadata = await GetLatestVersion(
            alternatePackage.PackageId,
            // Start from 0.0.0 to get any version
            new(0, 0, 0),
            sources,
            cache);

        if (alternateMetadata == null)
        {
            Log.Warning(
                "Package {Package} is deprecated with alternative {Alternative}, but alternative not found in sources",
                currentPackage,
                alternatePackage.PackageId);
            return null;
        }

        // Perform migration: update Include attribute and Version
        packageElement.SetAttributeValue("Include", alternatePackage.PackageId);

        // Use the minimum version from the range if specified and greater than 0.0.0,
        // otherwise use the latest version we found
        var minVersion = alternatePackage.Range?.MinVersion;
        var targetVersion = minVersion != null && minVersion > new NuGetVersion(0, 0, 0)
            ? minVersion
            : alternateMetadata.Identity.Version;
        packageElement.SetAttributeValue("Version", targetVersion.ToString());

        Log.Information(
            "Migrated {OldPackage} -> {NewPackage} (Version: {Version}) [Deprecated: {Reasons}]",
            currentPackage,
            alternatePackage.PackageId,
            targetVersion,
            string.Join(", ", deprecation.Reasons));

        return (currentPackage, alternatePackage.PackageId);
    }

    static async Task UpdateCsprojFiles(string directory, List<(string OldPackage, string NewPackage)> migrations)
    {
        // Find all csproj files recursively
        var csprojFiles = EnumerateCsprojFiles(directory);

        foreach (var csprojPath in csprojFiles)
        {
            var updated = false;
            var (newLine, hasTrailingNewline) = DetectNewLineInfo(csprojPath);
            var csprojXml = XDocument.Load(csprojPath);

            foreach (var (oldPackage, newPackage) in migrations)
            {
                var packageReferences = csprojXml.Descendants("PackageReference")
                    .Where(_ => string.Equals(
                        _.Attribute("Include")?.Value,
                        oldPackage,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var packageRef in packageReferences)
                {
                    packageRef.SetAttributeValue("Include", newPackage);
                    updated = true;
                    Log.Information(
                        "Updated PackageReference {OldPackage} -> {NewPackage} in {File}",
                        oldPackage,
                        newPackage,
                        Path.GetFileName(csprojPath));
                }
            }

            if (updated)
            {
                var xmlSettings = new XmlWriterSettings
                {
                    OmitXmlDeclaration = true,
                    Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    Indent = true,
                    IndentChars = "  ",
                    NewLineChars = newLine,
                    Async = true
                };

                await using (var writer = XmlWriter.Create(csprojPath, xmlSettings))
                {
                    await csprojXml.SaveAsync(writer, Cancel.None);
                }

                if (hasTrailingNewline)
                {
                    await File.AppendAllTextAsync(csprojPath, newLine);
                }
            }
        }
    }

    static IEnumerable<string> EnumerateCsprojFiles(string directory)
    {
        var stack = new Stack<string>();
        stack.Push(directory);

        while (stack.TryPop(out var current))
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current, "*.csproj", SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException)
            {
                files = [];
            }

            foreach (var file in files)
            {
                yield return file;
            }

            IEnumerable<string> subdirectories;
            try
            {
                subdirectories = Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException)
            {
                subdirectories = [];
            }

            foreach (var subdirectory in subdirectories)
            {
                stack.Push(subdirectory);
            }
        }
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
            .Where(_ => ShouldConsiderVersion(_, currentVersion))
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
