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

    // Progress<T> reports on the thread pool, out of order; this keeps them as they happen.
    class Recorder : IProgress<UpdateProgress>
    {
        public List<UpdateProgress> Reports { get; } = [];

        public void Report(UpdateProgress value) =>
            Reports.Add(value);
    }
}
