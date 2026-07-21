public static class ToolUpdater
{
    public static async Task Update(
        SourceCacheContext cache,
        string manifestPath,
        string? packageName)
    {
        var bytes = await File.ReadAllBytesAsync(manifestPath);

        List<ToolEntry> tools;
        try
        {
            tools = ToolManifest.Read(bytes);
        }
        catch (JsonException exception)
        {
            Log.Warning(
                "Failed to parse tool manifest {FilePath}. Error: {Message}",
                manifestPath,
                exception.Message);
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
        var sources = PackageSourceReader.Read(directory);

        var updates = new List<(ToolEntry Tool, NuGetVersion Version)>();

        foreach (var tool in candidates)
        {
            if (!NuGetVersion.TryParse(tool.Version, out var currentVersion))
            {
                continue;
            }

            var latestMetadata = await Updater.GetLatestVersion(
                tool.Package,
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

            updates.Add((tool, latestVersion));
            Log.Information("Updated tool {Package}: {NuGetVersion} -> {LatestVersion}", tool.Package, currentVersion, latestVersion);
        }

        if (updates.Count == 0)
        {
            return;
        }

        await File.WriteAllBytesAsync(manifestPath, ToolManifest.ApplyUpdates(bytes, updates));
    }
}
