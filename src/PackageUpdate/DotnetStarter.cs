using System;
using System.Threading.Tasks;

static class DotnetStarter
{
    public static async Task<string[]> StartDotNet(string arguments)
    {
        var result = await ProcessHelper.RunProcessAsync("dotnet", arguments, 10000);
        Console.WriteLine($"    dotnet {arguments}");
        if (result.ExitCode != 0)
        {
            throw new Exception($@"Command: dotnet {arguments}
Timed out");
        }

        return result.Output.Split(new[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries);
    }
}