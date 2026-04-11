static class ForkDetector
{
    static ConcurrentDictionary<string, bool> cache = new(StringComparer.OrdinalIgnoreCase);

    public static bool ShouldSkip(string targetDirectory, string solutionPath)
    {
        var solutionDir = Path.GetDirectoryName(solutionPath)!;
        var gitRoot = FindGitRoot(solutionDir);
        if (gitRoot == null)
        {
            return false;
        }

        if (!cache.GetOrAdd(gitRoot, HasUpstreamRemote))
        {
            return false;
        }

        var normalizedTarget = Path.GetFullPath(targetDirectory);
        var normalizedGitRoot = Path.GetFullPath(gitRoot);

        // Skip if target is above the git root (fork was discovered via scanning)
        // Don't skip if target is at or below the git root (fork was explicitly targeted)
        return !normalizedTarget.Equals(normalizedGitRoot, StringComparison.OrdinalIgnoreCase) &&
               !normalizedTarget.StartsWith(normalizedGitRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    static string? FindGitRoot(string directory)
    {
        var current = new DirectoryInfo(directory);
        while (current != null)
        {
            var gitPath = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    static bool HasUpstreamRemote(string gitRoot)
    {
        var configPath = Path.Combine(gitRoot, ".git", "config");
        if (!File.Exists(configPath))
        {
            return false;
        }

        var content = File.ReadAllText(configPath);
        return content.Contains("[remote \"upstream\"]", StringComparison.OrdinalIgnoreCase);
    }
}
