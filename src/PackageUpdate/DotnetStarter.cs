using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

static class DotnetStarter
{
    public static List<string> StartDotNet(string arguments, string directory)
    {
        using (var process = new Process
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
        })
        {
            process.Start();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                var output = process.StandardOutput.ReadToEnd();
                throw new Exception($@"Command: dotnet {arguments}
WorkingDirectory: {directory}
ExitCode: {process.ExitCode}
Error: {error}
Output: {output}");
            }

            return process.ReadLines().ToList();
        }
    }
}