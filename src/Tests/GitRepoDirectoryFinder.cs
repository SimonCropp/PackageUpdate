using System.Diagnostics.CodeAnalysis;

static class GitRepoDirectoryFinder
{
    public static string Find()
    {
        if (TryFind(out var rootDirectory))
        {
            return rootDirectory;
        }

        throw new("Could not find root git directory");
    }

    public static bool TryFind([NotNullWhen(true)] out string? path)
    {
        var currentDirectory = AssemblyLocation.CurrentDirectory;
        do
        {
            if (Directory.Exists(Path.Combine(currentDirectory, ".git")))
            {
                path = currentDirectory;
                return true;
            }

            var parent = Directory.GetParent(currentDirectory);
            if (parent == null)
            {
                path = null;
                return false;
            }

            currentDirectory = parent.FullName;
        } while (true);
    }
}