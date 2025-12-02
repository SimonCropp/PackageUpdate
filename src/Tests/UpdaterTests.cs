public class UpdaterTests
{
    [Fact]
    public async Task UpdateAllPackages()
    {
        var content = """
                      <Project>
                        <PropertyGroup>
                          <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                        </PropertyGroup>
                        <ItemGroup>
                          <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" />
                          <PackageVersion Include="NUnit" Version="3.13.0" />
                        </ItemGroup>
                      </Project>
                      """;

        using var tempFile = await TempFile.CreateText(content);

        await Updater.Update(tempFile.Path, null);

        var result = await File.ReadAllTextAsync(tempFile.Path);
        await Verify(result);
    }

    [Fact]
    public async Task UpdateSinglePackage()
    {
        var content = """
                      <Project>
                        <PropertyGroup>
                          <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                        </PropertyGroup>
                        <ItemGroup>
                          <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" />
                          <PackageVersion Include="NUnit" Version="3.13.0" />
                        </ItemGroup>
                      </Project>
                      """;

        using var tempFile = await TempFile.CreateText(content);

        await Updater.Update(
            tempFile.Path,
            "Newtonsoft.Json");

        var result = await File.ReadAllTextAsync(tempFile.Path);
        await Verify(result);
    }

    [Fact]
    public async Task UpdateSinglePackage_CaseInsensitive()
    {
        var content = """
                      <Project>
                        <ItemGroup>
                          <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" />
                          <PackageVersion Include="NUnit" Version="3.13.0" />
                        </ItemGroup>
                      </Project>
                      """;

        using var tempFile = await TempFile.CreateText(content);

        await Updater.Update(
            tempFile.Path,
            "newtonsoft.json");

        var result = await File.ReadAllTextAsync(tempFile.Path);
        await Verify(result);
    }

    [Fact]
    public async Task UpdatePackageNotFound()
    {
        var content = """
                      <Project>
                        <ItemGroup>
                          <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" />
                        </ItemGroup>
                      </Project>
                      """;

        using var tempFile = await TempFile.CreateText(content);

        await Updater.Update(
            tempFile.Path,
            "NonExistentPackage");

        var result = await File.ReadAllTextAsync(tempFile.Path);

        // File should be unchanged
        Assert.Equal(content, result);
    }

    [Fact]
    public async Task PreservesFormatting()
    {
        var content = """
                      <Project>
                        <!-- This is a comment -->
                        <PropertyGroup>
                          <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                        </PropertyGroup>

                        <ItemGroup>
                          <!-- Testing packages -->
                          <PackageVersion Include="NUnit" Version="3.13.0" />
                        </ItemGroup>
                      </Project>
                      """;

        using var tempFile = await TempFile.CreateText(content);

        await Updater.Update(tempFile.Path, null);

        var result = await File.ReadAllTextAsync(tempFile.Path);

        // Verify comments are preserved
        Assert.Contains("<!-- This is a comment -->", result);
        Assert.Contains("<!-- Testing packages -->", result);
    }

    [Fact]
    public async Task SkipsInvalidVersions()
    {
        var content = """
                      <Project>
                        <ItemGroup>
                          <PackageVersion Include="ValidPackage" Version="1.0.0" />
                          <PackageVersion Include="InvalidPackage" Version="not-a-version" />
                        </ItemGroup>
                      </Project>
                      """;

        using var tempFile = await TempFile.CreateText(content);

        await Updater.Update(tempFile.Path, null);

        var result = await File.ReadAllTextAsync(tempFile.Path);

        // Invalid version should remain unchanged
        Assert.Contains("not-a-version", result);
    }

    static List<PackageSource> sources = [new("https://api.nuget.org/v3/index.json")];

    [Fact]
    public async Task GetLatestVersion_StableToStable()
    {
        var cache = new SourceCacheContext();
        var currentVersion = NuGetVersion.Parse("12.0.1");

        var result = await Updater.GetLatestVersion(
            "Newtonsoft.Json",
            currentVersion,
            sources,
            cache);

        Assert.NotNull(result);
        Assert.True(result.Identity.Version > currentVersion);
        Assert.False(result.Identity.Version.IsPrerelease);
    }

    [Fact]
    public async Task GetLatestVersion_PreReleaseToPreRelease()
    {
        var cache = new SourceCacheContext();
        var currentVersion = NuGetVersion.Parse("1.0.0-beta.1");

        var result = await Updater.GetLatestVersion(
            "Verify",
            currentVersion,
            sources,
            cache);

        Assert.NotNull(result);
        Assert.True(result.Identity.Version > currentVersion);
    }

    [Fact]
    public async Task GetLatestVersion_DoesNotDowngradeStableToPreRelease()
    {
        var cache = new SourceCacheContext();
        var currentVersion = NuGetVersion.Parse("13.0.1");

        var result = await Updater.GetLatestVersion(
            "Newtonsoft.Json",
            currentVersion,
            sources,
            cache);

        if (result != null)
        {
            Assert.False(result.Identity.Version.IsPrerelease);
        }
    }

    [Fact]
    public async Task GetLatestVersion_PackageNotFound()
    {
        var cache = new SourceCacheContext();
        var currentVersion = NuGetVersion.Parse("1.0.0");

        var result = await Updater.GetLatestVersion(
            "ThisPackageDefinitelyDoesNotExist12345",
            currentVersion,
            sources,
            cache);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestVersion_AlreadyLatest()
    {
        var cache = new SourceCacheContext();

        // Use a very high version number
        var currentVersion = NuGetVersion.Parse("999.999.999");

        var result = await Updater.GetLatestVersion(
            "Newtonsoft.Json",
            currentVersion,
            sources,
            cache);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestVersion_ReturnsMetadata()
    {
        var cache = new SourceCacheContext();
        var currentVersion = NuGetVersion.Parse("12.0.1");

        var result = await Updater.GetLatestVersion(
            "Newtonsoft.Json",
            currentVersion,
            sources,
            cache);

        Assert.NotNull(result);

        var metadata = new
        {
            result.Identity.Id,
            Version = result.Identity.Version.ToString(),
            HasVersion = result.Identity.Version != null,
            result.Identity.Version?.IsPrerelease
        };

        await Verify(metadata);
    }

    [Fact]
    public async Task UsesLocalNuGetConfig()
    {
        var nugetConfig = """
                          <?xml version="1.0" encoding="utf-8"?>
                          <configuration>
                            <packageSources>
                              <clear />
                              <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
                            </packageSources>
                          </configuration>
                          """;

        var packages = """
                       <Project>
                         <ItemGroup>
                           <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" />
                         </ItemGroup>
                       </Project>
                       """;

        using var directory = new TempDirectory();
        var nugetConfigPath = Path.Combine(directory, "nuget.config");
        var packagesPath = Path.Combine(directory, "Directory.Packages.props");

        await File.WriteAllTextAsync(nugetConfigPath, nugetConfig);
        await File.WriteAllTextAsync(packagesPath, packages);

        await Updater.Update(packagesPath, null);

        var result = await File.ReadAllTextAsync(packagesPath);

        // Verify the package was updated (should have a newer version than 12.0.1)
        Assert.DoesNotContain("Version=\"12.0.1\"", result);
    }

    [Fact]
    public async Task WarnsAndReturnsWhenNoNuGetConfig()
    {
        var packages = """
                       <Project>
                         <ItemGroup>
                           <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" />
                         </ItemGroup>
                       </Project>
                       """;

        using var directory = new TempDirectory();
        var packagesPath = Path.Combine(directory, "Directory.Packages.props");

        await File.WriteAllTextAsync(packagesPath, packages);

        // Should not throw, just log warning and return
        await Updater.Update(packagesPath, null);

        var result = await File.ReadAllTextAsync(packagesPath);

        // Verify the package was updated (should have a newer version than 12.0.1)
        Assert.DoesNotContain("Version=\"12.0.1\"", result);
    }

    [Fact]
    public async Task UsesLocalNuGetConfigInHierarchy()
    {
        var nugetConfig = """
                          <?xml version="1.0" encoding="utf-8"?>
                          <configuration>
                            <packageSources>
                              <clear />
                              <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
                            </packageSources>
                          </configuration>
                          """;

        var packages = """
                       <Project>
                         <ItemGroup>
                           <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" />
                         </ItemGroup>
                       </Project>
                       """;

        using var directory = new TempDirectory();
        var nugetConfigPath = Path.Combine(directory, "nuget.config");
        var directoryPath = Path.Combine(directory, "Directory.Packages.props");

        await File.WriteAllTextAsync(nugetConfigPath, nugetConfig);
        await File.WriteAllTextAsync(directoryPath, packages);

        await Updater.Update(directoryPath, null);

        var result = await File.ReadAllTextAsync(directoryPath);

        // Verify the package was updated using the local config merged with hierarchy
        Assert.DoesNotContain("Version=\"12.0.1\"", result);
    }

    [Fact]
    public async Task GetLatestVersion_IgnoresUnlistedPackages()
    {
        var cache = new SourceCacheContext();

        // YoloDev.Expecto.TestSdk v1.0.0 is unlisted
        // Start from a version before 1.0.0 to see if it skips the unlisted version
        var currentVersion = NuGetVersion.Parse("0.1.0");

        var result = await Updater.GetLatestVersion(
            "YoloDev.Expecto.TestSdk",
            currentVersion,
            sources,
            cache);

        // Should find a listed version, skipping 1.0.0 (unlisted)
        Assert.NotNull(result);
        Assert.True(result.IsListed, "Returned package version should be listed");
        Assert.NotEqual("1.0.0", result.Identity.Version.ToString());
        Assert.True(result.Identity.Version > currentVersion);
    }

    [Fact]
    public async Task UpdateSkipsUnlistedVersions()
    {
        var nugetConfig = """
                          <?xml version="1.0" encoding="utf-8"?>
                          <configuration>
                            <packageSources>
                              <clear />
                              <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
                            </packageSources>
                          </configuration>
                          """;

        var packages = """
                       <Project>
                         <ItemGroup>
                           <PackageVersion Include="YoloDev.Expecto.TestSdk" Version="0.1.0" />
                         </ItemGroup>
                       </Project>
                       """;

        using var directory = new TempDirectory();
        var nugetConfigPath = Path.Combine(directory, "nuget.config");
        var packagesPath = Path.Combine(directory, "Directory.Packages.props");

        await File.WriteAllTextAsync(nugetConfigPath, nugetConfig);
        await File.WriteAllTextAsync(packagesPath, packages);

        await Updater.Update(packagesPath, null);

        var result = await File.ReadAllTextAsync(packagesPath);
        var doc = XDocument.Parse(result);

        var packageVersion = doc.Descendants("PackageVersion")
            .FirstOrDefault(e => e.Attribute("Include")?.Value == "YoloDev.Expecto.TestSdk");

        Assert.NotNull(packageVersion);

        var versionAttr = packageVersion.Attribute("Version")?.Value;

        // Should have updated, but NOT to the unlisted 1.0.0
        Assert.NotEqual("0.1.0", versionAttr);
        Assert.NotEqual("1.0.0", versionAttr);

        Assert.True(NuGetVersion.TryParse(versionAttr, out var updatedVersion));
        Assert.True(updatedVersion > NuGetVersion.Parse("0.1.0"));
    }

    [Fact]
    public async Task UpdateRespectsPinnedPackages()
    {
        var nugetConfig = """
                          <?xml version="1.0" encoding="utf-8"?>
                          <configuration>
                            <packageSources>
                              <clear />
                              <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
                            </packageSources>
                          </configuration>
                          """;

        var packages = """
                       <Project>
                         <ItemGroup>
                           <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" Update="false" />
                           <PackageVersion Include="NUnit" Version="3.13.0" />
                         </ItemGroup>
                       </Project>
                       """;

        using var directory = new TempDirectory();
        var nugetConfigPath = Path.Combine(directory, "nuget.config");
        var packagesPath = Path.Combine(directory, "Directory.Packages.props");

        await File.WriteAllTextAsync(nugetConfigPath, nugetConfig);
        await File.WriteAllTextAsync(packagesPath, packages);

        await Updater.Update(packagesPath, null);

        var result = await File.ReadAllTextAsync(packagesPath);

        // Pinned package should not be updated
        Assert.Contains("Newtonsoft.Json\" Version=\"12.0.1\"", result);
        Assert.Contains("Update=\"false\"", result);

        // Non-pinned package should be updated
        Assert.DoesNotContain("NUnit\" Version=\"3.13.0\"", result);
    }

    [Fact]
    public async Task UpdateAllPackagesArePinned()
    {
        var packages = """
                       <Project>
                         <ItemGroup>
                           <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" Update="false" />
                           <PackageVersion Include="NUnit" Version="3.13.0" Update="false" />
                         </ItemGroup>
                       </Project>
                       """;

        using var tempFile = await TempFile.CreateText(packages);

        await Updater.Update(tempFile.Path, null);

        var result = await File.ReadAllTextAsync(tempFile.Path);

        // All packages should remain unchanged
        Assert.Contains("Newtonsoft.Json\" Version=\"12.0.1\"", result);
        Assert.Contains("NUnit\" Version=\"3.13.0\"", result);
        Assert.Contains("Update=\"false\"", result);
    }

    [Fact]
    public async Task UpdateSinglePinnedPackage()
    {
        var packages = """
                       <Project>
                         <ItemGroup>
                           <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" Update="false" />
                           <PackageVersion Include="NUnit" Version="3.13.0" />
                         </ItemGroup>
                       </Project>
                       """;

        using var tempFile = await TempFile.CreateText(packages);

        // Try to update the pinned package specifically
        await Updater.Update(
            tempFile.Path,
            "Newtonsoft.Json");

        var result = await File.ReadAllTextAsync(tempFile.Path);

        // Should still be pinned and not updated
        Assert.Contains("Newtonsoft.Json\" Version=\"12.0.1\"", result);
        Assert.Contains("Update=\"false\"", result);
    }

    [Fact]
    public async Task UpdatePreservesPinAttributeFormat()
    {
        var packages = """
                       <Project>
                         <ItemGroup>
                           <!-- Important: keep this version locked -->
                           <PackageVersion Include="System.ValueTuple" Version="4.5.0" Update="false" />
                           <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" />
                         </ItemGroup>
                       </Project>
                       """;

        using var tempFile = await TempFile.CreateText(packages);

        await Updater.Update(tempFile.Path, null);

        var result = await File.ReadAllTextAsync(tempFile.Path);

        // Verify comment is preserved
        Assert.Contains("<!-- Important: keep this version locked -->", result);

        // Verify pinned package wasn't updated
        Assert.Contains("System.ValueTuple\" Version=\"4.5.0\"", result);
        Assert.Contains("Update=\"false\"", result);
    }

    [Fact]
    public async Task UpdateOnlyUnpinnedPackagesUpdated()
    {
        var nugetConfig = """
                          <?xml version="1.0" encoding="utf-8"?>
                          <configuration>
                            <packageSources>
                              <clear />
                              <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
                            </packageSources>
                          </configuration>
                          """;

        var directoryPackages = """
                                <Project>
                                  <ItemGroup>
                                    <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" Update="false" />
                                    <PackageVersion Include="NUnit" Version="3.13.0" Update="false" />
                                    <PackageVersion Include="xunit" Version="2.4.0" />
                                  </ItemGroup>
                                </Project>
                                """;

        using var directory = new TempDirectory();
        var nugetConfigPath = Path.Combine(directory, "nuget.config");
        var packagesPath = Path.Combine(directory, "Directory.Packages.props");

        await File.WriteAllTextAsync(nugetConfigPath, nugetConfig);
        await File.WriteAllTextAsync(packagesPath, directoryPackages);

        await Updater.Update(packagesPath, null);

        var result = await File.ReadAllTextAsync(packagesPath);
        var doc = XDocument.Parse(result);

        var packages = doc.Descendants("PackageVersion")
            .Select(element => new
            {
                Id = element.Attribute("Include")?.Value,
                Version = element.Attribute("Version")?.Value,
                Update = element.Attribute("Update")?.Value
            })
            .ToList();

        // Pinned packages unchanged
        var newtonsoft = packages.First(_ => _.Id == "Newtonsoft.Json");
        Assert.Equal("12.0.1", newtonsoft.Version);
        Assert.Equal("false", newtonsoft.Update);

        var nunit = packages.First(_ => _.Id == "NUnit");
        Assert.Equal("3.13.0", nunit.Version);
        Assert.Equal("false", nunit.Update);

        // Unpinned package updated
        var xunit = packages.First(_ => _.Id == "xunit");
        Assert.NotEqual("2.4.0", xunit.Version);
        Assert.Null(xunit.Update);
    }
}