public static class ToolUpdater
{
    /// <summary>
    /// Updates the tool versions in a dotnet-tools.json.
    /// </summary>
    /// <param name="packageStarted">Told each tool, its position from 1, and how many there are, as checking it starts.</param>
    /// <param name="logger">Told what is updated, skipped, or fails. Nothing is logged when null.</param>
    /// <returns>What was changed, and what was skipped.</returns>
    public static async Task<UpdateResult> Update(
        SourceCacheContext cache,
        string manifestPath,
        string? packageName,
        Action<string, int, int>? packageStarted = null,
        ILogger? logger = null)
    {
        var result = new UpdateResult();
        await Update(cache, manifestPath, packageName, packageStarted, logger ?? NullLogger.Instance, result);
        return result;
    }

    internal static async Task Update(
        SourceCacheContext cache,
        string manifestPath,
        string? packageName,
        Action<string, int, int>? packageStarted,
        ILogger logger,
        UpdateResult result)
    {
        var bytes = await File.ReadAllBytesAsync(manifestPath);

        List<ToolEntry> tools;
        try
        {
            tools = ToolManifest.Read(bytes);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                "Failed to parse tool manifest {FilePath}. Error: {Message}",
                manifestPath,
                exception.Message);
            result.Add(
                new UpdateProblem(
                    UpdateProblemKind.ManifestUnreadable,
                    manifestPath,
                    $"Failed to parse tool manifest. Error: {exception.Message}"));
            return;
        }

        var candidates = tools
            .Where(_ => !_.Pinned)
            .ToList();

        // Filter to specific package if requested
        if (!string.IsNullOrEmpty(packageName))
        {
            candidates = candidates
                .Where(_ => string.Equals(_.Package, packageName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (candidates.Count == 0)
        {
            return;
        }

        var directory = Path.GetDirectoryName(manifestPath)!;
        var sources = PackageSourceReader.ReadConfigured(directory);

        var updates = new List<(ToolEntry Tool, NuGetVersion Version)>();

        // Added to the result once the file is written
        var changes = new List<PackageChange>();

        var toolNumber = 0;
        foreach (var tool in candidates)
        {
            toolNumber++;
            packageStarted?.Invoke(tool.Package, toolNumber, candidates.Count);

            if (!NuGetVersion.TryParse(tool.Version, out var currentVersion))
            {
                continue;
            }

            if (Updater.ReportIfUnmapped(sources, tool.Package, manifestPath, logger, result))
            {
                continue;
            }

            var latestMetadata = await Updater.GetLatestVersion(
                tool.Package,
                currentVersion,
                sources.For(tool.Package),
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

            updates.Add((tool, latestVersion));
            logger.LogInformation("Updated tool {Package}: {NuGetVersion} -> {LatestVersion}", tool.Package, currentVersion, latestVersion);
            changes.Add(new(PackageChangeKind.Tool, manifestPath, tool.Package, currentVersion, latestVersion));
        }

        if (updates.Count == 0)
        {
            return;
        }

        await File.WriteAllBytesAsync(manifestPath, ToolManifest.ApplyUpdates(bytes, updates));
        foreach (var change in changes)
        {
            result.Add(change);
        }
    }
}
