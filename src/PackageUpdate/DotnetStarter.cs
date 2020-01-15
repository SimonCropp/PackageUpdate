using System;
using System.Threading.Tasks;

static class DotnetStarter
{
    public static async Task<string[]> StartDotNet(string arguments)
    {
        var result = await ProcessHelper.RunProcessAsync("dotnet", arguments, 10000);
        Console.WriteLine($"    dotnet {arguments}");
        if (result.HasFailed)
        {
            throw new Exception($@"Command failed: dotnet {arguments}
Killed: {result.Killed}
ExitCode: {result.ExitCode}
Output: {result.Output}
Error: {result.Error}");
        }

        return result.Output.Split(new[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries);
    }
}