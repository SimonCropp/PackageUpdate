static class FileSystem
{
    public static string? FindPropsFile(string startDirectory, string stopDirectory)
    {
        var stop = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stopDirectory));
        for (var current = new DirectoryInfo(startDirectory); current is not null; current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "Directory.Packages.props");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            if (string.Equals(current.FullName, stop, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        return null;
    }

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
