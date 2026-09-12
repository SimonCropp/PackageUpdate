static class PropsHelper
{
    internal static string CreateSolutionDir(string root, string relativePath)
    {
        var dir = Path.Combine(root, relativePath);
        Directory.CreateDirectory(dir);
        return dir;
    }

    internal static async Task<string> CreateProps(string directory)
    {
        var propsPath = Path.Combine(directory, "Directory.Packages.props");
        await File.WriteAllTextAsync(propsPath, "<Project />");
        return propsPath;
    }
}
