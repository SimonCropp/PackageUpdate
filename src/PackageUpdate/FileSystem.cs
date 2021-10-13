static class FileSystem
{
    public static IEnumerable<string> EnumerateFiles(string directory, string pattern)
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
            return Directory.EnumerateFiles(directory,pattern, SearchOption.TopDirectoryOnly);
        }
        catch (UnauthorizedAccessException)
        {
            return Enumerable.Empty<string>();
        }
    }

    static IEnumerable<string> GetDirectories(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory,"*", SearchOption.TopDirectoryOnly);
        }
        catch (UnauthorizedAccessException)
        {
            return Enumerable.Empty<string>();
        }
    }
}