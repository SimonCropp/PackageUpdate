static class DotnetStarter
{
    public static async Task<List<string>> StartDotNet(string arguments, string directory)
    {
        using var process = new Process
        {
            StartInfo = new()
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = directory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.Start();
        Console.WriteLine($"    dotnet {arguments}");
        if (!process.WaitForExit(60000))
        {
            throw new($@"Command: dotnet {arguments}
Timed out
WorkingDirectory: {directory}");
        }

        if (process.ExitCode == 0)
        {
            return await process.ReadLines();
        }

        var error = await process.StandardError.ReadToEndAsync();
        var output = await process.StandardOutput.ReadToEndAsync();
        throw new($@"Command: dotnet {arguments}
WorkingDirectory: {directory}
ExitCode: {process.ExitCode}
Error: {error}
Output: {output}");
    }
}