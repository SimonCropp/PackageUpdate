using Microsoft.Extensions.Logging.Abstractions;

public class DotnetStarterTests
{
    [Test]
    public async Task Build_PathWithSpaces()
    {
        using var temp = new TempDirectory();
        var directory = Path.Combine(temp, "with spaces");
        Directory.CreateDirectory(directory);
        var project = await WriteProject(directory, """<Message Importance="high" Text="Built" />""");

        await DotnetStarter.Build(project, NullLogger.Instance);
    }

    [Test]
    public async Task Build_OutputLargerThanPipeBuffer()
    {
        using var temp = new TempDirectory();
        // One message per item, so well over the 4KB (Windows) and 64KB (Linux) pipe buffers
        var lines = string.Join(';', Enumerable.Range(0, 2000));
        var project = await WriteProject(
            temp,
            $"""
             <ItemGroup>
               <Line Include="{lines}" />
             </ItemGroup>
             <Message Importance="high" Text="Line %(Line.Identity) of output written by the build to fill the pipe" />
             """);

        await DotnetStarter.Build(project, NullLogger.Instance);
    }

    [Test]
    public async Task Build_FailureThrowsWithOutput()
    {
        using var temp = new TempDirectory();
        var project = await WriteProject(temp, """<Error Text="Expected failure" />""");

        var build = () => DotnetStarter.Build(project, NullLogger.Instance);

        var exception = await Assert.That(build).Throws<Exception>();
        await Assert.That(exception!.Message).Contains("Expected failure");
    }

    // A project with no SDK, so building it needs no restore or compile. dotnet build runs the
    // default target, which without DefaultTargets is the first, Restore
    static async Task<string> WriteProject(string directory, string buildTarget)
    {
        var path = Path.Combine(directory, "Project.proj");
        await File.WriteAllTextAsync(
            path,
            $"""
             <Project DefaultTargets="Build">
               <Target Name="Restore" />
               <Target Name="Build">
                 {buildTarget}
               </Target>
             </Project>
             """);
        return path;
    }
}
