static class DotnetStarter
{
    public static Task Build(string solution, ILogger logger)
    {
        logger.LogInformation("    Build {Solution}", solution);
        return StartDotNet(
            arguments: $"build {solution} --nologo",
            logger: logger,
            directory: Directory.GetParent(solution)!.FullName);
    }

    public static Task Shutdown(ILogger logger)
    {
        logger.LogInformation("Shutdown dotnet build");
        return StartDotNet(
            arguments: "build-server shutdown",
            logger: logger,
            directory: Environment.CurrentDirectory);
    }

    static async Task<List<string>> StartDotNet(string arguments, ILogger logger, string directory)
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
        logger.LogInformation("    dotnet {Arguments}", arguments);

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