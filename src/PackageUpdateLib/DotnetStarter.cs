static class DotnetStarter
{
    static TimeSpan timeout = TimeSpan.FromSeconds(60);

    public static Task Build(string solution, ILogger logger)
    {
        logger.LogInformation("    Build {Solution}", solution);
        return StartDotNet(
            arguments: ["build", solution, "--nologo"],
            logger: logger,
            directory: Directory.GetParent(solution)!.FullName);
    }

    public static Task Shutdown(ILogger logger)
    {
        logger.LogInformation("Shutdown dotnet build");
        return StartDotNet(
            arguments: ["build-server", "shutdown"],
            logger: logger,
            directory: Environment.CurrentDirectory);
    }

    static async Task StartDotNet(string[] arguments, ILogger logger, string directory)
    {
        // ArgumentList quotes each argument, so a path with spaces stays one argument
        var startInfo = new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        var command = $"dotnet {string.Join(' ', arguments)}";

        using var process = new Process();
        process.StartInfo = startInfo;
        process.Start();
        logger.LogInformation("    {Command}", command);

        // Read both streams while waiting. Otherwise, once the output fills the pipe buffer,
        // dotnet blocks on writing it and never exits
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();

        using var cancel = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cancel.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(true);
            throw new(
                $"""
                 Command: {command}
                 Timed out
                 WorkingDirectory: {directory}
                 """);
        }

        await Task.WhenAll(output, error);

        if (process.ExitCode == 0)
        {
            return;
        }

        throw new(
            $"""
             Command: {command}
             WorkingDirectory: {directory}
             ExitCode: {process.ExitCode}
             Error: {await error}
             Output: {await output}
             """);
    }
}
