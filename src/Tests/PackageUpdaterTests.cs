public class PackageUpdaterTests
{
    [Test]
    public async Task ReportsProgress()
    {
        using var directory = new TempDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(directory, "nuget.config"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
              </packageSources>
            </configuration>
            """);
        await File.WriteAllTextAsync(Path.Combine(directory, "Sample.slnx"), "<Solution />");
        await File.WriteAllTextAsync(
            Path.Combine(directory, "Directory.Packages.props"),
            """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" />
                <PackageVersion Include="NUnit" Version="3.13.0" />
                <PackageVersion Include="Pinned" Version="1.0.0" Pinned="true" />
              </ItemGroup>
            </Project>
            """);
        var config = Path.Combine(directory, ".config");
        Directory.CreateDirectory(config);
        await File.WriteAllTextAsync(
            Path.Combine(config, "dotnet-tools.json"),
            """
            {
              "version": 1,
              "isRoot": true,
              "tools": {
                "dotnet-ef": {
                  "version": "5.0.0",
                  "commands": [
                    "dotnet-ef"
                  ]
                }
              }
            }
            """);

        var progress = new Recorder();
        await PackageUpdater.Run(directory, null, false, progress);

        await Verify(
            progress.Reports.Select(_ => _ with
            {
                File = Path.GetRelativePath(directory, _.File).Replace('\\', '/')
            }));
    }

    // Each kind of change, and each problem Run reports itself rather than one from Updater.
    // Latest versions move, so only that each went up is checked, not what to.
    [Test]
    public async Task ReturnsWhatChangedAndWhatWentWrong()
    {
        using var directory = new TempDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(directory, "nuget.config"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
              </packageSources>
            </configuration>
            """);

        // A central version, a VersionOverride, and a tool, all updated
        var good = Directory.CreateDirectory(Path.Combine(directory, "Good")).FullName;
        await File.WriteAllTextAsync(Path.Combine(good, "Good.slnx"), "<Solution />");
        await File.WriteAllTextAsync(
            Path.Combine(good, "Directory.Packages.props"),
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" />
                <PackageVersion Include="Pinned" Version="1.0.0" Pinned="true" />
              </ItemGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(
            Path.Combine(good, "Good.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="NUnit" VersionOverride="3.13.0" />
              </ItemGroup>
            </Project>
            """);
        var config = Directory.CreateDirectory(Path.Combine(directory, ".config")).FullName;
        await File.WriteAllTextAsync(
            Path.Combine(config, "dotnet-tools.json"),
            """
            {
              "version": 1,
              "isRoot": true,
              "tools": {
                "dotnet-ef": {
                  "version": "5.0.0",
                  "commands": [
                    "dotnet-ef"
                  ]
                }
              }
            }
            """);

        // A solution with no Directory.Packages.props
        var old = Directory.CreateDirectory(Path.Combine(directory, "Old")).FullName;
        await File.WriteAllTextAsync(Path.Combine(old, "Old.sln"), "");

        // A solution whose Directory.Packages.props is not XML, so working on it throws
        var bad = Directory.CreateDirectory(Path.Combine(directory, "Bad")).FullName;
        await File.WriteAllTextAsync(Path.Combine(bad, "Bad.slnx"), "<Solution />");
        await File.WriteAllTextAsync(Path.Combine(bad, "Directory.Packages.props"), "<Project>");

        // A tool manifest that is not JSON
        var broken = Directory.CreateDirectory(Path.Combine(directory, "Broken")).FullName;
        await File.WriteAllTextAsync(Path.Combine(broken, "dotnet-tools.json"), "{ not json");

        var result = await PackageUpdater.Run(directory, null, false);

        await Verify(
            new
            {
                result.Succeeded,
                Changes = result.Changes
                    .Select(_ => new
                    {
                        Kind = _.Kind.ToString(),
                        File = Relative(directory, _.File),
                        _.Package,
                        From = _.From.ToString(),
                        WentUp = _.To > _.From
                    })
                    .OrderBy(_ => _.File),
                Problems = result.Problems
                    .Select(_ => new
                    {
                        Kind = _.Kind.ToString(),
                        File = Relative(directory, _.File),
                        _.Message,
                        _.IsFailure,
                        Exception = _.Exception?.GetType().Name
                    })
                    .OrderBy(_ => _.File)
            });
    }

    static string Relative(string directory, string file) =>
        Path.GetRelativePath(directory, file).Replace('\\', '/');

    // Progress<T> reports on the thread pool, out of order; this keeps them as they happen.
    class Recorder : IProgress<UpdateProgress>
    {
        public List<UpdateProgress> Reports { get; } = [];

        public void Report(UpdateProgress value) =>
            Reports.Add(value);
    }
}
