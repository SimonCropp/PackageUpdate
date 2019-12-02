using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

static class DotnetStarter
{
    public static async Task<List<string>> StartDotNet(string arguments, string directory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
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
        process.WaitForExit();
        if (process.ExitCode == 0)
        {
            return await process.ReadLines();
        }

        var error = await process.StandardError.ReadToEndAsync();
        var output = await process.StandardOutput.ReadToEndAsync();
        throw new Exception($@"Command: dotnet {arguments}
WorkingDirectory: {directory}
ExitCode: {process.ExitCode}
Error: {error}
Output: {output}");
    }
}