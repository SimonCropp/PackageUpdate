public class ToolUpdaterTests
{
    static string nugetConfig =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
          </packageSources>
        </configuration>
        """;

    static async Task<string> RunScenario(string manifest, string? package = null)
    {
        using var cache = new SourceCacheContext
        {
            RefreshMemoryCache = true
        };

        using var directory = new TempDirectory();
        var config = Path.Combine(directory, ".config");
        Directory.CreateDirectory(config);

        await File.WriteAllTextAsync(Path.Combine(directory, "nuget.config"), nugetConfig);

        var manifestPath = Path.Combine(config, "dotnet-tools.json");
        await File.WriteAllTextAsync(manifestPath, manifest);

        await ToolUpdater.Update(cache, manifestPath, package);

        return await File.ReadAllTextAsync(manifestPath);
    }

    static string? VersionOf(string manifest, string tool) =>
        JsonDocument.Parse(manifest)
            .RootElement
            .GetProperty("tools")
            .GetProperty(tool)
            .GetProperty("version")
            .GetString();

    [Test]
    public async Task UpdatesToolVersion()
    {
        var manifest =
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
            """;

        var result = await RunScenario(manifest);

        var version = VersionOf(result, "dotnet-ef");
        await Assert.That(NuGetVersion.TryParse(version, out var updated)).IsTrue();
        await Assert.That(updated! > NuGetVersion.Parse("5.0.0")).IsTrue();

        // A stable version must never move to a pre-release
        await Assert.That(updated!.IsPrerelease).IsFalse();
    }

    [Test]
    public async Task PreservesEverythingExceptTheVersion()
    {
        var manifest =
            """
            {
              "version": 1,
              "isRoot": true,
              "tools": {
                "dotnet-ef": {
                  "version": "5.0.0",
                  "commands": [
                    "dotnet-ef"
                  ],
                  "rollForward": false
                }
              }
            }
            """;

        var result = await RunScenario(manifest);

        var version = VersionOf(result, "dotnet-ef");
        await Assert.That(version).IsNotEqualTo("5.0.0");

        // Byte for byte identical apart from the single version value
        await Assert.That(result).IsEqualTo(manifest.Replace("\"5.0.0\"", $"\"{version}\""));
    }

    [Test]
    public async Task PreservesNonStandardFormatting()
    {
        // Tab indented, no trailing newline
        var manifest = "{\n\t\"version\": 1,\n\t\"tools\": {\n\t\t\"dotnet-ef\": {\"version\":\"5.0.0\"}\n\t}\n}";

        var result = await RunScenario(manifest);

        var version = VersionOf(result, "dotnet-ef");
        await Assert.That(version).IsNotEqualTo("5.0.0");
        await Assert.That(result).IsEqualTo(manifest.Replace("\"5.0.0\"", $"\"{version}\""));
    }

    [Test]
    public async Task RespectsPinnedTool()
    {
        var manifest =
            """
            {
              "version": 1,
              "isRoot": true,
              "tools": {
                "dotnet-ef": {
                  "version": "5.0.0",
                  "commands": [
                    "dotnet-ef"
                  ],
                  "pinned": true
                },
                "packageupdate": {
                  "version": "4.0.0",
                  "commands": [
                    "packageupdate"
                  ]
                }
              }
            }
            """;

        var result = await RunScenario(manifest);

        await Assert.That(VersionOf(result, "dotnet-ef")).IsEqualTo("5.0.0");
        await Assert.That(VersionOf(result, "packageupdate")).IsNotEqualTo("4.0.0");
    }

    [Test]
    public async Task RespectsPackageFilter()
    {
        var manifest =
            """
            {
              "version": 1,
              "tools": {
                "dotnet-ef": {
                  "version": "5.0.0"
                },
                "packageupdate": {
                  "version": "4.0.0"
                }
              }
            }
            """;

        var result = await RunScenario(manifest, "packageupdate");

        await Assert.That(VersionOf(result, "dotnet-ef")).IsEqualTo("5.0.0");
        await Assert.That(VersionOf(result, "packageupdate")).IsNotEqualTo("4.0.0");
    }

    [Test]
    public async Task PackageFilterIsCaseInsensitive()
    {
        var manifest =
            """
            {
              "version": 1,
              "tools": {
                "packageupdate": {
                  "version": "4.0.0"
                }
              }
            }
            """;

        var result = await RunScenario(manifest, "PackageUpdate");

        await Assert.That(VersionOf(result, "packageupdate")).IsNotEqualTo("4.0.0");
    }

    [Test]
    public async Task SkipsInvalidVersion()
    {
        var manifest =
            """
            {
              "version": 1,
              "tools": {
                "dotnet-ef": {
                  "version": "not-a-version"
                }
              }
            }
            """;

        var result = await RunScenario(manifest);

        await Assert.That(result).IsEqualTo(manifest);
    }

    [Test]
    public async Task SkipsUnknownTool()
    {
        var manifest =
            """
            {
              "version": 1,
              "tools": {
                "ThisToolDefinitelyDoesNotExist12345": {
                  "version": "1.0.0"
                }
              }
            }
            """;

        var result = await RunScenario(manifest);

        await Assert.That(result).IsEqualTo(manifest);
    }

    [Test]
    public async Task MalformedManifestIsLeftUnchanged()
    {
        var manifest = "{ not json";

        var result = await RunScenario(manifest);

        await Assert.That(result).IsEqualTo(manifest);
    }
}
