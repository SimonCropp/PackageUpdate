public class PackageSourceMappingTests
{
    // "public" has a 48 hour minimum age, "internal" has none, and both carry both packages.
    // Without mapping, Other.Package would take internal's 2.0.0, and Contoso.Lib public's 3.0.0.
    // Mapped, each is looked up only in its own source: Other.Package stops at public's newest old
    // enough version, 1.1.0, and Contoso.Lib takes internal's 2.0.0. Unmapped.Package can come from
    // neither, so it is reported and left alone.
    [Test]
    public async Task Update_LooksUpOnlyMappedSources()
    {
        using var directory = new TempDirectory();
        var publicFeed = Path.Combine(directory, "public");
        var internalFeed = Path.Combine(directory, "internal");
        Directory.CreateDirectory(publicFeed);
        Directory.CreateDirectory(internalFeed);
        var tenDaysAgo = DateTime.UtcNow.AddDays(-10);
        LocalFeed.WritePackage(publicFeed, "Other.Package", "1.0.0", tenDaysAgo);
        LocalFeed.WritePackage(publicFeed, "Other.Package", "1.1.0", tenDaysAgo);
        LocalFeed.WritePackage(publicFeed, "Other.Package", "2.0.0");
        LocalFeed.WritePackage(publicFeed, "Contoso.Lib", "3.0.0", tenDaysAgo);
        LocalFeed.WritePackage(internalFeed, "Other.Package", "2.0.0");
        LocalFeed.WritePackage(internalFeed, "Contoso.Lib", "2.0.0");

        await File.WriteAllTextAsync(
            Path.Combine(directory, "nuget.config"),
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <configuration>
               <packageSources>
                 <clear />
                 <add key="public" value="{publicFeed}" minPublishAgeHours="48" />
                 <add key="internal" value="{internalFeed}" />
               </packageSources>
               <packageSourceMapping>
                 <packageSource key="public">
                   <package pattern="Other.*" />
                 </packageSource>
                 <packageSource key="internal">
                   <package pattern="Contoso.*" />
                 </packageSource>
               </packageSourceMapping>
             </configuration>
             """);
        var props = Path.Combine(directory, "Directory.Packages.props");
        await File.WriteAllTextAsync(
            props,
            """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Other.Package" Version="1.0.0" />
                <PackageVersion Include="Contoso.Lib" Version="1.0.0" />
                <PackageVersion Include="Unmapped.Package" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);
        var csproj = Path.Combine(directory, "Sample.csproj");
        await File.WriteAllTextAsync(
            csproj,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Other.Package" VersionOverride="1.0.0" />
                <PackageReference Include="Unmapped.Package" VersionOverride="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        using var cache = new SourceCacheContext
        {
            RefreshMemoryCache = true
        };
        var result = await Updater.Update(cache, props, null);

        var versions = result.Changes
            .Where(_ => _.Kind == PackageChangeKind.Version)
            .ToDictionary(_ => _.Package, _ => _.To.ToString());
        await Assert.That(versions.Count).IsEqualTo(2);
        await Assert.That(versions["Other.Package"]).IsEqualTo("1.1.0");
        await Assert.That(versions["Contoso.Lib"]).IsEqualTo("2.0.0");

        var versionOverride = result.Changes.Single(_ => _.Kind == PackageChangeKind.VersionOverride);
        await Assert.That(versionOverride.Package).IsEqualTo("Other.Package");
        await Assert.That(versionOverride.To.ToString()).IsEqualTo("1.1.0");

        await Assert.That(result.Problems.Count).IsEqualTo(2);
        foreach (var problem in result.Problems)
        {
            await Assert.That(problem.Kind).IsEqualTo(UpdateProblemKind.NoMappedSource);
            await Assert.That(problem.Package).IsEqualTo("Unmapped.Package");
        }

        await Assert.That(result.Problems.Select(_ => _.File)).IsEquivalentTo([props, csproj]);
        await Assert.That(result.Succeeded).IsTrue();
    }

    [Test]
    public async Task ToolUpdate_LooksUpOnlyMappedSources()
    {
        using var directory = new TempDirectory();
        var feed = Path.Combine(directory, "feed");
        Directory.CreateDirectory(feed);
        LocalFeed.WritePackage(feed, "Mapped.Tool", "1.0.0");
        LocalFeed.WritePackage(feed, "Mapped.Tool", "2.0.0");
        LocalFeed.WritePackage(feed, "Unmapped.Tool", "1.0.0");
        LocalFeed.WritePackage(feed, "Unmapped.Tool", "2.0.0");

        await File.WriteAllTextAsync(
            Path.Combine(directory, "nuget.config"),
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <configuration>
               <packageSources>
                 <clear />
                 <add key="local" value="{feed}" />
               </packageSources>
               <packageSourceMapping>
                 <packageSource key="local">
                   <package pattern="Mapped.*" />
                 </packageSource>
               </packageSourceMapping>
             </configuration>
             """);
        var manifestPath = Path.Combine(directory, "dotnet-tools.json");
        await File.WriteAllTextAsync(
            manifestPath,
            """
            {
              "version": 1,
              "isRoot": true,
              "tools": {
                "Mapped.Tool": {
                  "version": "1.0.0",
                  "commands": [
                    "mapped"
                  ]
                },
                "Unmapped.Tool": {
                  "version": "1.0.0",
                  "commands": [
                    "unmapped"
                  ]
                }
              }
            }
            """);

        using var cache = new SourceCacheContext
        {
            RefreshMemoryCache = true
        };
        var result = await ToolUpdater.Update(cache, manifestPath, null);

        var change = result.Changes.Single();
        await Assert.That(change.Package).IsEqualTo("Mapped.Tool");
        await Assert.That(change.To.ToString()).IsEqualTo("2.0.0");

        var problem = result.Problems.Single();
        await Assert.That(problem.Kind).IsEqualTo(UpdateProblemKind.NoMappedSource);
        await Assert.That(problem.Package).IsEqualTo("Unmapped.Tool");
        await Assert.That(problem.File).IsEqualTo(manifestPath);
    }
}
