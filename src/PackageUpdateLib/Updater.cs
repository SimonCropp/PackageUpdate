public static class Updater
{
    /// <summary>
    /// Updates the versions in a Directory.Packages.props, migrating deprecated packages, then the
    /// VersionOverrides in the csproj files under it.
    /// </summary>
    /// <param name="packageStarted">Told each package, its position from 1, and how many there are, as checking it starts.</param>
    /// <param name="logger">Told what is updated, skipped, or fails. Nothing is logged when null.</param>
    /// <returns>What was changed, and what was skipped.</returns>
    public static async Task<UpdateResult> Update(
        SourceCacheContext cache,
        string directoryPackagesPropsPath,
        string? packageName,
        Action<string, int, int>? packageStarted = null,
        ILogger? logger = null)
    {
        var result = new UpdateResult();
        await Update(cache, directoryPackagesPropsPath, packageName, packageStarted, logger ?? NullLogger.Instance, result);
        return result;
    }

    // Adds to the result as each file is saved, so a later file throwing still leaves what was written.
    internal static async Task Update(
        SourceCacheContext cache,
        string directoryPackagesPropsPath,
        string? packageName,
        Action<string, int, int>? packageStarted,
        ILogger logger,
        UpdateResult result)
    {
        var directory = Path.GetDirectoryName(directoryPackagesPropsPath)!;

        // Load the XML document. The editor splices updates into the original text so
        // formatting, including attributes spread over multiple lines, is preserved
        var editor = XmlEditor.Load(directoryPackagesPropsPath);
        var xml = editor.Document;

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
                logger.LogWarning("Package {Package} not found in {FilePath}", packageName, directoryPackagesPropsPath);
                result.Add(
                    new UpdateProblem(UpdateProblemKind.PackageNotFound, directoryPackagesPropsPath, $"Package {packageName} not found")
                    {
                        Package = packageName
                    });
                return;
            }
        }

        var sources = PackageSourceReader.ReadConfigured(directory);

        // Added to the result once the file is saved
        var changes = new List<PackageChange>();

        // Track migrations for csproj updates. Projects is the list in the migration's change, so each
        // csproj is added to it as it is saved.
        var migrations = new List<(string OldPackage, string NewPackage, List<string> Projects)>();

        // Update each package
        var packageNumber = 0;
        foreach (var package in packageVersions)
        {
            packageNumber++;
            packageStarted?.Invoke(package.Package!, packageNumber, packageVersions.Count);

            if (!NuGetVersion.TryParse(package.CurrentVersion, out var currentVersion))
            {
                continue;
            }

            // Check if current version is deprecated and attempt migration
            var currentMetadata = await GetPackageMetadata(
                package.Package!,
                currentVersion,
                sources.For(package.Package!),
                cache,
                logger);

            if (currentMetadata != null)
            {
                var deprecation = await currentMetadata.GetDeprecationMetadataAsync();
                if (deprecation != null)
                {
                    var migration = await TryMigratePackage(
                        editor,
                        package.Element,
                        package.Package!,
                        deprecation,
                        sources,
                        cache,
                        xml,
                        directoryPackagesPropsPath,
                        logger,
                        result);

                    if (migration != null)
                    {
                        var (alternative, alternativeVersion) = migration.Value;
                        var projects = new List<string>();
                        changes.Add(
                            new(PackageChangeKind.Migration, directoryPackagesPropsPath, package.Package!, currentVersion, alternativeVersion)
                            {
                                Alternative = alternative,
                                Projects = projects
                            });
                        migrations.Add((package.Package!, alternative, projects));
                        // Migration successful, skip normal version update
                        continue;
                    }
                    // If migration failed, continue with normal version update
                }
            }

            var latestMetadata = await GetLatestVersion(
                package.Package!,
                currentVersion,
                sources.For(package.Package!),
                cache,
                logger);

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
            editor.SetAttribute(package.Element, "Version", latestVersion.ToString());
            logger.LogInformation("Updated {Package}: {NuGetVersion} -> {LatestVersion}", package.Package, currentVersion, latestVersion);
            changes.Add(new(PackageChangeKind.Version, directoryPackagesPropsPath, package.Package!, currentVersion, latestVersion));
        }

        await editor.Save(directoryPackagesPropsPath);
        foreach (var change in changes)
        {
            result.Add(change);
        }

        // Update PackageReference entries in csproj files for migrated packages
        if (migrations.Count > 0)
        {
            await UpdateCsprojFiles(directory, migrations, logger);
        }

        // Update VersionOverride entries in csproj files (e.g. test projects tracking a pre-release)
        await UpdateVersionOverrides(directory, packageName, sources, cache, logger, result);
    }

    /// <summary>
    /// The latest version to move to from the current version, or null when there is none.
    /// The result is reused for the life of the cache, for the same sources.
    /// </summary>
    public static async Task<IPackageSearchMetadata?> GetLatestVersion(
        string package,
        NuGetVersion currentVersion,
        List<PackageSource> sources,
        SourceCacheContext cache,
        ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        var latestVersionCache = LookupCache.For(cache, sources).Latest;
        var key = (package, currentVersion);
        if (latestVersionCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        IPackageSearchMetadata? latestMetadata = null;

        foreach (var source in sources)
        {
            var (repository, metadataResource) = await RepositoryReader.Read(source);

            var condidates = await GetCondidates(package, currentVersion, cache, repository, logger);

            foreach (var candidate in condidates)
            {
                var metadata = await metadataResource.GetMetadataAsync(
                    new(package, candidate),
                    cache,
                    new NuGetLogger(logger),
                    Cancel.None);

                // Skip unlisted packages
                if (metadata is not {IsListed: true})
                {
                    continue;
                }

                // Skip versions newer than the source's minPublishAgeHours cooldown
                var cooldownSkip = CooldownSkipReason(metadata.Published, source.MinPublishAge, DateTimeOffset.UtcNow);
                if (cooldownSkip != null)
                {
                    logger.LogInformation(
                        "Skipped {Package} {Version} from {Source}: {Reason}",
                        package,
                        candidate,
                        source.Name,
                        cooldownSkip);
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

    // Why a version cannot be selected under the source's minimum publish age, or null when it can.
    // NuGet errors on a version with no publish date rather than assume it is old enough. It is
    // skipped here instead, so one version without a date does not fail the whole file.
    internal static string? CooldownSkipReason(DateTimeOffset? published, TimeSpan minPublishAge, DateTimeOffset now)
    {
        if (minPublishAge <= TimeSpan.Zero)
        {
            return null;
        }

        if (published == null)
        {
            return "no publish date to check against minPublishAgeHours";
        }

        if (now - published.Value < minPublishAge)
        {
            return "published within minPublishAgeHours";
        }

        return null;
    }

    static async Task<IPackageSearchMetadata?> GetPackageMetadata(
        string package,
        NuGetVersion version,
        List<PackageSource> sources,
        SourceCacheContext cache,
        ILogger logger)
    {
        var metadataCache = LookupCache.For(cache, sources).Metadata;
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
                new NuGetLogger(logger),
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

    static async Task<(string Alternative, NuGetVersion Version)?> TryMigratePackage(
        XmlEditor editor,
        XElement packageElement,
        string currentPackage,
        PackageDeprecationMetadata deprecation,
        ConfiguredSources sources,
        SourceCacheContext cache,
        XDocument xml,
        string directoryPackagesPropsPath,
        ILogger logger,
        UpdateResult result)
    {
        var reasons = string.Join(", ", deprecation.Reasons);

        // Check if alternate package exists
        var alternateId = deprecation.AlternatePackage?.PackageId;
        if (alternateId == null)
        {
            logger.LogWarning(
                "Package {Package} is deprecated but has no alternative. Reasons: {Reasons}",
                currentPackage,
                reasons);
            result.Add(
                new UpdateProblem(
                    UpdateProblemKind.DeprecatedWithoutAlternative,
                    directoryPackagesPropsPath,
                    $"Package {currentPackage} is deprecated but has no alternative. Reasons: {reasons}")
                {
                    Package = currentPackage
                });
            return null;
        }

        // Check if alternate already exists in Directory.Packages.props
        var existingAlternate = xml.Descendants("PackageVersion")
            .FirstOrDefault(_ =>
                string.Equals(
                    _.Attribute("Include")?.Value,
                    alternateId,
                    StringComparison.OrdinalIgnoreCase));

        if (existingAlternate != null)
        {
            logger.LogWarning(
                "Package {Package} is deprecated with alternative {Alternative}, but alternative already exists",
                currentPackage,
                alternateId);
            result.Add(
                new UpdateProblem(
                    UpdateProblemKind.AlternativeAlreadyPresent,
                    directoryPackagesPropsPath,
                    $"Package {currentPackage} is deprecated with alternative {alternateId}, but alternative already exists")
                {
                    Package = currentPackage
                });
            return null;
        }

        // Verify alternate package exists in NuGet sources
        var alternateMetadata = await GetLatestVersion(
            alternateId,
            // Start from 0.0.0 to get any version
            new(0, 0, 0),
            sources.For(alternateId),
            cache,
            logger);

        if (alternateMetadata == null)
        {
            logger.LogWarning(
                "Package {Package} is deprecated with alternative {Alternative}, but alternative not found in sources",
                currentPackage,
                alternateId);
            result.Add(
                new UpdateProblem(
                    UpdateProblemKind.AlternativeNotFound,
                    directoryPackagesPropsPath,
                    $"Package {currentPackage} is deprecated with alternative {alternateId}, but alternative not found in sources")
                {
                    Package = currentPackage
                });
            return null;
        }

        // Perform migration: update Include attribute and Version
        editor.SetAttribute(packageElement, "Include", alternateId);

        // Always use the latest version of the alternate package
        var targetVersion = alternateMetadata.Identity.Version;
        editor.SetAttribute(packageElement, "Version", targetVersion.ToString());

        logger.LogInformation(
            "Migrated {OldPackage} -> {NewPackage} (Version: {Version}) [Deprecated: {Reasons}]",
            currentPackage,
            alternateId,
            targetVersion,
            reasons);

        return (alternateId, targetVersion);
    }

    static async Task UpdateCsprojFiles(string directory, List<(string OldPackage, string NewPackage, List<string> Projects)> migrations, ILogger logger)
    {
        // Find all csproj files recursively
        var csprojFiles = EnumerateCsprojFiles(directory);

        foreach (var csprojPath in csprojFiles)
        {
            var editor = XmlEditor.Load(csprojPath);

            // The Projects of each migration that renamed a reference in this csproj
            var renamedIn = new List<List<string>>();

            foreach (var (oldPackage, newPackage, projects) in migrations)
            {
                var packageReferences = editor.Document.Descendants("PackageReference")
                    .Where(_ => string.Equals(
                        _.Attribute("Include")?.Value,
                        oldPackage,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var packageRef in packageReferences)
                {
                    editor.SetAttribute(packageRef, "Include", newPackage);
                    logger.LogInformation(
                        "Updated PackageReference {OldPackage} -> {NewPackage} in {File}",
                        oldPackage,
                        newPackage,
                        Path.GetFileName(csprojPath));
                }

                if (packageReferences.Count > 0)
                {
                    renamedIn.Add(projects);
                }
            }

            await editor.Save(csprojPath);
            foreach (var projects in renamedIn)
            {
                projects.Add(csprojPath);
            }
        }
    }

    static async Task UpdateVersionOverrides(
        string directory,
        string? packageName,
        ConfiguredSources sources,
        SourceCacheContext cache,
        ILogger logger,
        UpdateResult result)
    {
        foreach (var csprojPath in EnumerateCsprojFiles(directory))
        {
            var editor = XmlEditor.Load(csprojPath);

            // Added to the result once the file is saved
            var changes = new List<PackageChange>();

            var overrides = editor.Document.Descendants("PackageReference")
                .Select(element => new
                {
                    Element = element,
                    Package = element.Attribute("Include")?.Value,
                    CurrentOverride = element.Attribute("VersionOverride")?.Value,
                    Pinned = element.Attribute("Pinned")?.Value == "true"
                })
                .Where(_ => _.Package != null &&
                            _.CurrentOverride != null &&
                            !_.Pinned)
                .ToList();

            foreach (var packageOverride in overrides)
            {
                // Filter to specific package if requested
                if (!string.IsNullOrEmpty(packageName) &&
                    !string.Equals(packageOverride.Package, packageName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!NuGetVersion.TryParse(packageOverride.CurrentOverride, out var currentVersion))
                {
                    continue;
                }

                // An override follows the same rule as a central version: a stable override only moves
                // to a newer stable, a pre-release override also considers pre-releases
                var latestMetadata = await GetLatestVersion(
                    packageOverride.Package!,
                    currentVersion,
                    sources.For(packageOverride.Package!),
                    cache,
                    logger);

                if (latestMetadata == null)
                {
                    continue;
                }

                var latestVersion = latestMetadata.Identity.Version;

                if (latestVersion <= currentVersion)
                {
                    continue;
                }

                editor.SetAttribute(packageOverride.Element, "VersionOverride", latestVersion.ToString());
                logger.LogInformation(
                    "Updated {Package} VersionOverride: {NuGetVersion} -> {LatestVersion} in {File}",
                    packageOverride.Package,
                    currentVersion,
                    latestVersion,
                    Path.GetFileName(csprojPath));
                changes.Add(new(PackageChangeKind.VersionOverride, csprojPath, packageOverride.Package!, currentVersion, latestVersion));
            }

            await editor.Save(csprojPath);
            foreach (var change in changes)
            {
                result.Add(change);
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

    static async Task<List<NuGetVersion>> GetCondidates(string package, NuGetVersion currentVersion, SourceCacheContext cache, SourceRepository repository, ILogger logger)
    {
        // Use FindPackageByIdResource to efficiently get version list
        var findResource = await repository.GetResourceAsync<FindPackageByIdResource>() ??
                           throw new($"Could not resolve a {nameof(FindPackageByIdResource)} for source {repository.PackageSource.Source}");

        var versions = await findResource.GetAllVersionsAsync(
            package,
            cache,
            new NuGetLogger(logger),
            Cancel.None);

        return versions
            .Where(_ => ShouldConsiderVersion(_, currentVersion))
            .OrderDescending()
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
