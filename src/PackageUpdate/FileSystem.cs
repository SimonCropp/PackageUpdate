static class FileSystem
{
    static StringComparison pathComparison = OperatingSystem.IsLinux() ?
        StringComparison.Ordinal :
        StringComparison.OrdinalIgnoreCase;

    public static string? FindPropsFile(string startDirectory, string stopDirectory)
    {
        var stop = Normalize(stopDirectory);

        // Never walk above the git root, otherwise a solution in a repo with no props file
        // can pick up the props file of a sibling or parent repo
        var gitRoot = FindGitRoot(startDirectory);
        if (gitRoot is not null &&
            IsInside(gitRoot, stop))
        {
            stop = gitRoot;
        }

        for (var current = new DirectoryInfo(startDirectory); current is not null; current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "Directory.Packages.props");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            // Stop at the boundary, and also bail out if the walk is not inside it at all,
            // which can happen for symlinked or otherwise non matching paths
            var directory = Normalize(current.FullName);
            if (string.Equals(directory, stop, pathComparison) ||
                !IsInside(directory, stop))
            {
                break;
            }
        }

        return null;
    }

    public static string? FindGitRoot(string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            var gitPath = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(gitPath) ||
                File.Exists(gitPath))
            {
                return Normalize(current.FullName);
            }
        }

        return null;
    }

    static string Normalize(string directory) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));

    static bool IsInside(string directory, string root) =>
        string.Equals(directory, root, pathComparison) ||
        directory.StartsWith(root + Path.DirectorySeparatorChar, pathComparison);

    public static IEnumerable<string> FindSolutions(string directory)
    {
        foreach (var solution in EnumerateFiles(directory, "*.sln"))
        {
            yield return solution;
        }

        foreach (var solution in EnumerateFiles(directory, "*.slnx"))
        {
            yield return solution;
        }
    }

    // Covers both `.config/dotnet-tools.json` and the legacy `dotnet-tools.json` beside a solution
    public static IEnumerable<string> FindToolManifests(string directory) =>
        EnumerateFiles(directory, "dotnet-tools.json");

    static List<string> EnumerateFiles(string directory, string pattern)
    {
        var allFiles = new List<string>();

        var stack = new Stack<string>();
        stack.Push(directory);

        while (stack.TryPop(out var current))
        {
            var files = GetFiles(current, pattern);
            allFiles.AddRange(files);

            foreach (var subdirectory in GetDirectories(current))
            {
                stack.Push(subdirectory);
            }
        }

        return allFiles;
    }

    static IEnumerable<string> GetFiles(string directory, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly);
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    static IEnumerable<string> GetDirectories(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly);
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }
}