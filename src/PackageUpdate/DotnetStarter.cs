static class DotnetStarter
{
    public static Task Build(string solution)
    {
        Log.Information("    Build {Solution}", solution);
        return StartDotNet(
            arguments: $"build {solution} --nologo",
            directory: Directory.GetParent(solution)!.FullName);
    }

    public static Task Shutdown()
    {
        Log.Information("Shutdown dotnet build");
        return StartDotNet(
            arguments: "build-server shutdown",
            directory: Environment.CurrentDirectory);
    }

    static async Task<List<string>> StartDotNet(string arguments, string directory)
    {
        using var process = new Process();
        process.StartInfo = new()
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = directory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        process.Start();
        Log.Information("    dotnet {Arguments}", arguments);

        if (!process.WaitForExit(60000))
        {
            process.Kill(true);
            throw new(
                $"""
                 Command: dotnet {arguments}
                 Timed out
                 WorkingDirectory: {directory}
                 """);
        }

        if (process.ExitCode == 0)
        {
            return await process.ReadLines();
        }

        var error = await process.StandardError.ReadToEndAsync();
        var output = await process.StandardOutput.ReadToEndAsync();
        throw new(
            $"""
             Command: dotnet {arguments}
             WorkingDirectory: {directory}
             ExitCode: {process.ExitCode}
             Error: {error}
             Output: {output}
             """);
    }
}