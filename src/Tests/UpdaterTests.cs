public class UpdaterTests
{
    [Test]
    public async Task UpdateAllPackages()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var content =
            """
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

        await Updater.Update(cache, tempFile.Path, null);

        var result = await File.ReadAllTextAsync(tempFile.Path);
        await Verify(result);
    }

    [Test]
    public async Task UpdateSinglePackage()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var content =
            """
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

        await Updater.Update(cache, tempFile.Path, "Newtonsoft.Json");

        var result = await File.ReadAllTextAsync(tempFile.Path);
        await Verify(result);
    }

    [Test]
    public async Task UpdateSinglePackage_CaseInsensitive()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var content =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" />
                <PackageVersion Include="NUnit" Version="3.13.0" />
              </ItemGroup>
            </Project>
            """;

        using var tempFile = await TempFile.CreateText(content);

        await Updater.Update(cache, tempFile.Path, "newtonsoft.json");

        var result = await File.ReadAllTextAsync(tempFile.Path);
        await Verify(result);
    }

    [Test]
    public async Task UpdatePackageNotFound()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var content =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" />
              </ItemGroup>
            </Project>
            """;

        using var tempFile = await TempFile.CreateText(content);

        await Updater.Update(cache, tempFile.Path, "NonExistentPackage");

        var result = await File.ReadAllTextAsync(tempFile.Path);

        // File should be unchanged
        await Assert.That(result).IsEqualTo(content);
    }

    [Test]
    public async Task PreservesFormatting()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var content =
            """
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

        await Updater.Update(cache, tempFile.Path, null);

        var result = await File.ReadAllTextAsync(tempFile.Path);

        // Verify comments are preserved
        await Assert.That(result).Contains("<!-- This is a comment -->");
        await Assert.That(result).Contains("<!-- Testing packages -->");
    }

    [Test]
    public async Task SkipsInvalidVersions()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var content =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="ValidPackage" Version="1.0.0" />
                <PackageVersion Include="InvalidPackage" Version="not-a-version" />
              </ItemGroup>
            </Project>
            """;

        using var tempFile = await TempFile.CreateText(content);

        await Updater.Update(cache, tempFile.Path, null);

        var result = await File.ReadAllTextAsync(tempFile.Path);

        // Invalid version should remain unchanged
        await Assert.That(result).Contains("not-a-version");
    }

    static List<PackageSource> sources = [new("https://api.nuget.org/v3/index.json")];

    [Test]
    public async Task GetLatestVersion_StableToStable()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var currentVersion = NuGetVersion.Parse("12.0.1");

        var result = await Updater.GetLatestVersion(
            "Newtonsoft.Json",
            currentVersion,
            sources,
            cache);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Identity.Version > currentVersion).IsTrue();
        await Assert.That(result.Identity.Version.IsPrerelease).IsFalse();
    }

    [Test]
    public async Task GetLatestVersion_PreReleaseToPreRelease()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var currentVersion = NuGetVersion.Parse("1.0.0-beta.1");

        var result = await Updater.GetLatestVersion(
            "Verify",
            currentVersion,
            sources,
            cache);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Identity.Version > currentVersion).IsTrue();
    }

    [Test]
    public async Task GetLatestVersion_DoesNotDowngradeStableToPreRelease()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var currentVersion = NuGetVersion.Parse("13.0.1");

        var result = await Updater.GetLatestVersion(
            "Newtonsoft.Json",
            currentVersion,
            sources,
            cache);

        if (result != null)
        {
            await Assert.That(result.Identity.Version.IsPrerelease).IsFalse();
        }
    }

    [Test]
    public async Task GetLatestVersion_PackageNotFound()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var currentVersion = NuGetVersion.Parse("1.0.0");

        var result = await Updater.GetLatestVersion(
            "ThisPackageDefinitelyDoesNotExist12345",
            currentVersion,
            sources,
            cache);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetLatestVersion_AlreadyLatest()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };

        // Use a very high version number
        var currentVersion = NuGetVersion.Parse("999.999.999");

        var result = await Updater.GetLatestVersion(
            "Newtonsoft.Json",
            currentVersion,
            sources,
            cache);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetLatestVersion_ReturnsMetadata()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var currentVersion = NuGetVersion.Parse("12.0.1");

        var result = await Updater.GetLatestVersion(
            "Newtonsoft.Json",
            currentVersion,
            sources,
            cache);

        await Assert.That(result).IsNotNull();

        var metadata = new
        {
            result.Identity.Id,
            Version = result.Identity.Version.ToString(),
            HasVersion = result.Identity.Version != null,
            result.Identity.Version?.IsPrerelease
        };

        await Verify(metadata);
    }

    [Test]
    public async Task UsesLocalNuGetConfig()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var nugetConfig =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
              </packageSources>
            </configuration>
            """;

        var packages =
            """
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

        await Updater.Update(cache, packagesPath, null);

        var result = await File.ReadAllTextAsync(packagesPath);

        // Verify the package was updated (should have a newer version than 12.0.1)
        await Assert.That(result).DoesNotContain("Version=\"12.0.1\"");
    }

    [Test]
    public async Task WarnsAndReturnsWhenNoNuGetConfig()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var packages =
            """
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
        await Updater.Update(cache, packagesPath, null);

        var result = await File.ReadAllTextAsync(packagesPath);

        // Verify the package was updated (should have a newer version than 12.0.1)
        await Assert.That(result).DoesNotContain("Version=\"12.0.1\"");
    }

    [Test]
    public async Task UsesLocalNuGetConfigInHierarchy()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var nugetConfig =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
              </packageSources>
            </configuration>
            """;

        var packages =
            """
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

        await Updater.Update(cache, directoryPath, null);

        var result = await File.ReadAllTextAsync(directoryPath);

        // Verify the package was updated using the local config merged with hierarchy
        await Assert.That(result).DoesNotContain("Version=\"12.0.1\"");
    }

    [Test]
    public async Task GetLatestVersion_IgnoresUnlistedPackages()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        // YoloDev.Expecto.TestSdk v1.0.0 is unlisted
        // Start from a version before 1.0.0 to see if it skips the unlisted version
        var currentVersion = NuGetVersion.Parse("0.1.0");

        var result = await Updater.GetLatestVersion(
            "YoloDev.Expecto.TestSdk",
            currentVersion,
            sources,
            cache);

        // Should find a listed version, skipping 1.0.0 (unlisted)
        await Assert.That(result).IsNotNull();
        await Assert.That(result.IsListed).IsTrue();
        await Assert.That(result.Identity.Version.ToString()).IsNotEqualTo("1.0.0");
        await Assert.That(result.Identity.Version > currentVersion).IsTrue();
    }

    [Test]
    public async Task UpdateSkipsUnlistedVersions()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var nugetConfig =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
              </packageSources>
            </configuration>
            """;

        var packages =
            """
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

        await Updater.Update(cache, packagesPath, null);

        var result = await File.ReadAllTextAsync(packagesPath);
        var doc = XDocument.Parse(result);

        var packageVersion = doc.Descendants("PackageVersion")
            .FirstOrDefault(_ => _.Attribute("Include")?.Value == "YoloDev.Expecto.TestSdk");

        await Assert.That(packageVersion).IsNotNull();

        var versionAttr = packageVersion.Attribute("Version")?.Value;

        // Should have updated, but NOT to the unlisted 1.0.0
        await Assert.That(versionAttr).IsNotEqualTo("0.1.0");
        await Assert.That(versionAttr).IsNotEqualTo("1.0.0");

        await Assert.That(NuGetVersion.TryParse(versionAttr, out var updatedVersion)).IsTrue();
        await Assert.That(updatedVersion! > NuGetVersion.Parse("0.1.0")).IsTrue();
    }

    [Test]
    public async Task UpdateRespectsPinnedPackages()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var nugetConfig =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
              </packageSources>
            </configuration>
            """;

        var packages =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" Pinned="true" />
                <PackageVersion Include="NUnit" Version="3.13.0" />
              </ItemGroup>
            </Project>
            """;

        using var directory = new TempDirectory();
        var nugetConfigPath = Path.Combine(directory, "nuget.config");
        var packagesPath = Path.Combine(directory, "Directory.Packages.props");

        await File.WriteAllTextAsync(nugetConfigPath, nugetConfig);
        await File.WriteAllTextAsync(packagesPath, packages);

        await Updater.Update(cache, packagesPath, null);

        var result = await File.ReadAllTextAsync(packagesPath);

        // Pinned package should not be updated
        await Assert.That(result).Contains("Newtonsoft.Json\" Version=\"12.0.1\"");
        await Assert.That(result).Contains("Pinned=\"true\"");

        // Non-pinned package should be updated
        await Assert.That(result).DoesNotContain("NUnit\" Version=\"3.13.0\"");
    }

    [Test]
    public async Task UpdateAllPackagesArePinned()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var packages =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" Pinned="true" />
                <PackageVersion Include="NUnit" Version="3.13.0" Pinned="true" />
              </ItemGroup>
            </Project>
            """;

        using var tempFile = await TempFile.CreateText(packages);

        await Updater.Update(cache, tempFile.Path, null);

        var result = await File.ReadAllTextAsync(tempFile.Path);

        // All packages should remain unchanged
        await Assert.That(result).Contains("Newtonsoft.Json\" Version=\"12.0.1\"");
        await Assert.That(result).Contains("NUnit\" Version=\"3.13.0\"");
        await Assert.That(result).Contains("Pinned=\"true\"");
    }

    [Test]
    public async Task UpdateSinglePinnedPackage()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var packages =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" Pinned="true" />
                <PackageVersion Include="NUnit" Version="3.13.0" />
              </ItemGroup>
            </Project>
            """;

        using var tempFile = await TempFile.CreateText(packages);

        // Try to update the pinned package specifically
        await Updater.Update(cache, tempFile.Path, "Newtonsoft.Json");

        var result = await File.ReadAllTextAsync(tempFile.Path);

        // Should still be pinned and not updated
        await Assert.That(result).Contains("Newtonsoft.Json\" Version=\"12.0.1\"");
        await Assert.That(result).Contains("Pinned=\"true\"");
    }

    [Test]
    public async Task UpdatePreservesPinAttributeFormat()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var packages =
            """
            <Project>
              <ItemGroup>
                <!-- Important: keep this version locked -->
                <PackageVersion Include="System.ValueTuple" Version="4.5.0" Pinned="true" />
                <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" />
              </ItemGroup>
            </Project>
            """;

        using var tempFile = await TempFile.CreateText(packages);

        await Updater.Update(cache, tempFile.Path, null);

        var result = await File.ReadAllTextAsync(tempFile.Path);

        // Verify comment is preserved
        await Assert.That(result).Contains("<!-- Important: keep this version locked -->");

        // Verify pinned package wasn't updated
        await Assert.That(result).Contains("System.ValueTuple\" Version=\"4.5.0\"");
        await Assert.That(result).Contains("Pinned=\"true\"");
    }

    [Test]
    public async Task UpdateOnlyUnpinnedPackagesUpdated()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var nugetConfig =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
              </packageSources>
            </configuration>
            """;

        var directoryPackages =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" Pinned="true" />
                <PackageVersion Include="NUnit" Version="3.13.0" Pinned="true" />
                <PackageVersion Include="xunit" Version="2.4.0" />
              </ItemGroup>
            </Project>
            """;

        using var directory = new TempDirectory();
        var nugetConfigPath = Path.Combine(directory, "nuget.config");
        var packagesPath = Path.Combine(directory, "Directory.Packages.props");

        await File.WriteAllTextAsync(nugetConfigPath, nugetConfig);
        await File.WriteAllTextAsync(packagesPath, directoryPackages);

        await Updater.Update(cache, packagesPath, null);

        var result = await File.ReadAllTextAsync(packagesPath);
        var doc = XDocument.Parse(result);

        var packages = doc.Descendants("PackageVersion")
            .Select(element => new
            {
                Id = element.Attribute("Include")?.Value,
                Version = element.Attribute("Version")?.Value,
                Pinned = element.Attribute("Pinned")?.Value
            })
            .ToList();

        // Pinned packages unchanged
        var newtonsoft = packages.First(_ => _.Id == "Newtonsoft.Json");
        await Assert.That(newtonsoft.Version).IsEqualTo("12.0.1");
        await Assert.That(newtonsoft.Pinned).IsEqualTo("true");

        var nunit = packages.First(_ => _.Id == "NUnit");
        await Assert.That(nunit.Version).IsEqualTo("3.13.0");
        await Assert.That(nunit.Pinned).IsEqualTo("true");

        // Unpinned package updated
        var xunit = packages.First(_ => _.Id == "xunit");
        await Assert.That(xunit.Version).IsNotEqualTo("2.4.0");
        await Assert.That(xunit.Pinned).IsNull();
    }

    [Test]
    public async Task UpdatePreservesOriginalNewlineStyle()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var content = "<Project>\n  <ItemGroup>\n    <PackageVersion Include=\"System.ValueTuple\" Version=\"4.5.0\" Pinned=\"true\" />\n  </ItemGroup>\n</Project>\n";

        using var tempFile = await TempFile.CreateText(content);

        // Read the original file to verify it has \n newlines
        var originalBytes = await File.ReadAllBytesAsync(tempFile.Path);
        var originalText = Encoding.UTF8.GetString(originalBytes);

        // Verify original has Unix newlines (\n) and not Windows newlines (\r\n)
        await Assert.That(originalText).Contains("\n");
        await Assert.That(originalText).DoesNotContain("\r\n");

        await Updater.Update(cache, tempFile.Path, null);

        // Read the result
        var resultBytes = await File.ReadAllBytesAsync(tempFile.Path);
        var resultText = Encoding.UTF8.GetString(resultBytes);

        // Verify newline style is preserved (should still be Unix \n, not Windows \r\n)
        await Assert.That(resultText).Contains("\n");
        await Assert.That(resultText).DoesNotContain("\r\n");
    }

    [Test]
    public async Task UpdatePreservesWindowsNewlineStyle()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var content = "<Project>\r\n  <ItemGroup>\r\n    <PackageVersion Include=\"System.ValueTuple\" Version=\"4.5.0\" Pinned=\"true\" />\r\n  </ItemGroup>\r\n</Project>\r\n";

        using var tempFile = await TempFile.CreateText(content);

        // Read the original file to verify it has \r\n newlines
        var originalBytes = await File.ReadAllBytesAsync(tempFile.Path);
        var originalText = Encoding.UTF8.GetString(originalBytes);

        // Verify original has Windows newlines (\r\n)
        await Assert.That(originalText).Contains("\r\n");

        await Updater.Update(cache, tempFile.Path, null);

        // Read the result
        var resultBytes = await File.ReadAllBytesAsync(tempFile.Path);
        var resultText = Encoding.UTF8.GetString(resultBytes);

        // Verify newline style is preserved (should still be Windows \r\n)
        await Assert.That(resultText).Contains("\r\n");
    }

    [Test]
    public async Task UpdatePreservesTrailingNewline()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        // Content WITH trailing newline
        var content = "<Project>\n  <ItemGroup>\n    <PackageVersion Include=\"System.ValueTuple\" Version=\"4.5.0\" Pinned=\"true\" />\n  </ItemGroup>\n</Project>\n";

        using var tempFile = await TempFile.CreateText(content);

        // Verify original ends with newline
        var originalBytes = await File.ReadAllBytesAsync(tempFile.Path);
        await Assert.That(originalBytes[^1]).IsEqualTo((byte)'\n');

        await Updater.Update(cache, tempFile.Path, null);

        // Verify result still ends with newline
        var resultBytes = await File.ReadAllBytesAsync(tempFile.Path);
        await Assert.That(resultBytes[^1]).IsEqualTo((byte)'\n');
    }

    [Test]
    public async Task UpdatePreservesNoTrailingNewline()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        // Content WITHOUT trailing newline
        var content = "<Project>\n  <ItemGroup>\n    <PackageVersion Include=\"System.ValueTuple\" Version=\"4.5.0\" Pinned=\"true\" />\n  </ItemGroup>\n</Project>";

        using var tempFile = await TempFile.CreateText(content);

        // Verify original does NOT end with newline
        var originalBytes = await File.ReadAllBytesAsync(tempFile.Path);
        await Assert.That(originalBytes[^1]).IsNotEqualTo((byte)'\n');
        await Assert.That(originalBytes[^1]).IsNotEqualTo((byte)'\r');

        await Updater.Update(cache, tempFile.Path, null);

        // Verify result still does NOT end with newline
        var resultBytes = await File.ReadAllBytesAsync(tempFile.Path);
        await Assert.That(resultBytes[^1]).IsNotEqualTo((byte)'\n');
        await Assert.That(resultBytes[^1]).IsNotEqualTo((byte)'\r');
    }

    [Test]
    public async Task UpdatePreservesTrailingCRLF()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        // Content WITH trailing CRLF
        var content = "<Project>\r\n  <ItemGroup>\r\n    <PackageVersion Include=\"System.ValueTuple\" Version=\"4.5.0\" Pinned=\"true\" />\r\n  </ItemGroup>\r\n</Project>\r\n";

        using var tempFile = await TempFile.CreateText(content);

        // Verify original ends with \r\n
        var originalBytes = await File.ReadAllBytesAsync(tempFile.Path);
        await Assert.That(originalBytes[^1]).IsEqualTo((byte)'\n');
        await Assert.That(originalBytes[^2]).IsEqualTo((byte)'\r');

        await Updater.Update(cache, tempFile.Path, null);

        // Verify result still ends with \r\n
        var resultBytes = await File.ReadAllBytesAsync(tempFile.Path);
        await Assert.That(resultBytes[^1]).IsEqualTo((byte)'\n');
        await Assert.That(resultBytes[^2]).IsEqualTo((byte)'\r');
    }

    [Test]
    public async Task UpdatePreservesNoTrailingNewlineWithCRLF()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        // Content with CRLF style but WITHOUT trailing newline
        var content = "<Project>\r\n  <ItemGroup>\r\n    <PackageVersion Include=\"System.ValueTuple\" Version=\"4.5.0\" Pinned=\"true\" />\r\n  </ItemGroup>\r\n</Project>";

        using var tempFile = await TempFile.CreateText(content);

        // Verify original does NOT end with newline
        var originalBytes = await File.ReadAllBytesAsync(tempFile.Path);
        await Assert.That(originalBytes[^1]).IsNotEqualTo((byte)'\n');
        await Assert.That(originalBytes[^1]).IsNotEqualTo((byte)'\r');

        await Updater.Update(cache, tempFile.Path, null);

        // Verify result still does NOT end with newline
        var resultBytes = await File.ReadAllBytesAsync(tempFile.Path);
        await Assert.That(resultBytes[^1]).IsNotEqualTo((byte)'\n');
        await Assert.That(resultBytes[^1]).IsNotEqualTo((byte)'\r');
    }

    [Test]
    public async Task MigratesDeprecatedPackageWithAlternative()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var content =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="WindowsAzure.Storage" Version="9.3.3" />
              </ItemGroup>
            </Project>
            """;

        using var tempFile = await TempFile.CreateText(content);

        await Updater.Update(cache, tempFile.Path, null);

        var result = await File.ReadAllTextAsync(tempFile.Path);
        var doc = XDocument.Parse(result);

        var packages = doc.Descendants("PackageVersion")
            .Select(element => new
            {
                Id = element.Attribute("Include")?.Value,
                Version = element.Attribute("Version")?.Value
            })
            .ToList();

        // Original package should be migrated to the alternative
        await Assert.That(packages).DoesNotContain(_ => _.Id == "WindowsAzure.Storage");

        // Alternative package should exist
        var alternativePackage = packages.FirstOrDefault(_ => _.Id == "Azure.Storage.Common" || _.Id == "Azure.Storage.Blobs");
        await Assert.That(alternativePackage).IsNotNull();
        await Assert.That(alternativePackage.Version).IsNotNull();
    }

    [Test]
    public async Task SkipsMigrationWhenAlternativeAlreadyExists()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var content =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="WindowsAzure.Storage" Version="9.3.3" />
                <PackageVersion Include="Azure.Storage.Common" Version="12.0.0" />
              </ItemGroup>
            </Project>
            """;

        using var tempFile = await TempFile.CreateText(content);

        await Updater.Update(cache, tempFile.Path, null);

        var result = await File.ReadAllTextAsync(tempFile.Path);

        // Use Verify to see the actual output
        await Verify(result);
    }

    [Test]
    public async Task PinnedDeprecatedPackageNotMigrated()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var content =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="WindowsAzure.Storage" Version="9.3.3" Pinned="true" />
              </ItemGroup>
            </Project>
            """;

        using var tempFile = await TempFile.CreateText(content);

        await Updater.Update(cache, tempFile.Path, null);

        var result = await File.ReadAllTextAsync(tempFile.Path);
        var doc = XDocument.Parse(result);

        var packages = doc.Descendants("PackageVersion")
            .Select(element => new
            {
                Id = element.Attribute("Include")?.Value,
                Version = element.Attribute("Version")?.Value,
                Pinned = element.Attribute("Pinned")?.Value
            })
            .ToList();

        // Pinned package should not be migrated
        var pinnedPackage = packages.FirstOrDefault(_ => _.Id == "WindowsAzure.Storage");
        await Assert.That(pinnedPackage).IsNotNull();
        await Assert.That(pinnedPackage.Version).IsEqualTo("9.3.3");
        await Assert.That(pinnedPackage.Pinned).IsEqualTo("true");
    }

    [Test]
    public async Task MigrationPreservesFormattingAndComments()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var content =
            """
            <Project>
              <!-- Important packages -->
              <ItemGroup>
                <!-- This one is deprecated -->
                <PackageVersion Include="WindowsAzure.Storage" Version="9.3.3" />
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """;

        using var tempFile = await TempFile.CreateText(content);

        await Updater.Update(cache, tempFile.Path, null);

        var result = await File.ReadAllTextAsync(tempFile.Path);

        // Verify comments are preserved
        await Assert.That(result).Contains("<!-- Important packages -->");
        await Assert.That(result).Contains("<!-- This one is deprecated -->");

        // Verify the package was migrated
        await Assert.That(result).DoesNotContain("WindowsAzure.Storage");
    }

    [Test]
    public async Task MigratedPackageNeverGetsZeroVersion()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var content =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="WindowsAzure.Storage" Version="9.3.3" />
              </ItemGroup>
            </Project>
            """;

        using var tempFile = await TempFile.CreateText(content);

        await Updater.Update(cache, tempFile.Path, null);

        var result = await File.ReadAllTextAsync(tempFile.Path);
        var doc = XDocument.Parse(result);

        var packages = doc.Descendants("PackageVersion")
            .Select(element => new
            {
                Id = element.Attribute("Include")?.Value,
                Version = element.Attribute("Version")?.Value
            })
            .ToList();

        // Verify no package has version 0.0.0
        // This test ensures that when deprecation metadata specifies MinVersion as 0.0.0
        // (open-ended range like "[,)"), we use the latest version instead
        foreach (var package in packages)
        {
            await Assert.That(package.Version).IsNotNull();
            await Assert.That(package.Version).IsNotEqualTo("0.0.0");

            // Verify it's a valid version
            await Assert.That(NuGetVersion.TryParse(package.Version, out var version)).IsTrue();
            await Assert.That(version! > new NuGetVersion(0, 0, 0)).IsTrue();
        }
    }

    [Test]
    public async Task MigrationUpdatesCsprojFiles()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var directoryPackages =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="WindowsAzure.Storage" Version="9.3.3" />
              </ItemGroup>
            </Project>
            """;

        var csproj =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="WindowsAzure.Storage" />
              </ItemGroup>
            </Project>
            """;

        using var directory = new TempDirectory();
        var packagesPath = Path.Combine(directory, "Directory.Packages.props");
        var csprojPath = Path.Combine(directory, "MyProject.csproj");

        await File.WriteAllTextAsync(packagesPath, directoryPackages);
        await File.WriteAllTextAsync(csprojPath, csproj);

        await Updater.Update(cache, packagesPath, null);

        var csprojResult = await File.ReadAllTextAsync(csprojPath);

        // Csproj should have the new package name
        await Assert.That(csprojResult).DoesNotContain("WindowsAzure.Storage");
        await Assert.That(
            csprojResult.Contains("Azure.Storage.Common") || csprojResult.Contains("Azure.Storage.Blobs")).IsTrue();
    }

    [Test]
    public async Task MigrationUpdatesMultipleCsprojFiles()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var directoryPackages =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="WindowsAzure.Storage" Version="9.3.3" />
              </ItemGroup>
            </Project>
            """;

        var csproj1 =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="WindowsAzure.Storage" />
              </ItemGroup>
            </Project>
            """;

        var csproj2 =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="WindowsAzure.Storage" />
                <PackageReference Include="Newtonsoft.Json" />
              </ItemGroup>
            </Project>
            """;

        using var directory = new TempDirectory();
        var packagesPath = Path.Combine(directory, "Directory.Packages.props");
        var csprojPath1 = Path.Combine(directory, "Project1.csproj");
        var csprojPath2 = Path.Combine(directory, "Project2.csproj");

        await File.WriteAllTextAsync(packagesPath, directoryPackages);
        await File.WriteAllTextAsync(csprojPath1, csproj1);
        await File.WriteAllTextAsync(csprojPath2, csproj2);

        await Updater.Update(cache, packagesPath, null);

        var csprojResult1 = await File.ReadAllTextAsync(csprojPath1);
        var csprojResult2 = await File.ReadAllTextAsync(csprojPath2);

        // Both csproj files should have the new package name
        await Assert.That(csprojResult1).DoesNotContain("WindowsAzure.Storage");
        await Assert.That(csprojResult2).DoesNotContain("WindowsAzure.Storage");

        // Second csproj should still have Newtonsoft.Json
        await Assert.That(csprojResult2).Contains("Newtonsoft.Json");
    }

    [Test]
    public async Task MigrationUpdatesCsprojInSubdirectory()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var directoryPackages =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="WindowsAzure.Storage" Version="9.3.3" />
              </ItemGroup>
            </Project>
            """;

        var csproj =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="WindowsAzure.Storage" />
              </ItemGroup>
            </Project>
            """;

        using var directory = new TempDirectory();
        var packagesPath = Path.Combine(directory, "Directory.Packages.props");
        var subDir = Path.Combine(directory, "src", "MyProject");
        Directory.CreateDirectory(subDir);
        var csprojPath = Path.Combine(subDir, "MyProject.csproj");

        await File.WriteAllTextAsync(packagesPath, directoryPackages);
        await File.WriteAllTextAsync(csprojPath, csproj);

        await Updater.Update(cache, packagesPath, null);

        var csprojResult = await File.ReadAllTextAsync(csprojPath);

        // Csproj in subdirectory should have the new package name
        await Assert.That(csprojResult).DoesNotContain("WindowsAzure.Storage");
    }

    [Test]
    public async Task MigrationPreservesCsprojFormatting()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var directoryPackages =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="WindowsAzure.Storage" Version="9.3.3" />
              </ItemGroup>
            </Project>
            """;

        var csproj =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <!-- Project comment -->
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <!-- Package references -->
                <PackageReference Include="WindowsAzure.Storage" />
              </ItemGroup>
            </Project>
            """;

        using var directory = new TempDirectory();
        var packagesPath = Path.Combine(directory, "Directory.Packages.props");
        var csprojPath = Path.Combine(directory, "MyProject.csproj");

        await File.WriteAllTextAsync(packagesPath, directoryPackages);
        await File.WriteAllTextAsync(csprojPath, csproj);

        await Updater.Update(cache, packagesPath, null);

        var csprojResult = await File.ReadAllTextAsync(csprojPath);

        // Comments should be preserved
        await Assert.That(csprojResult).Contains("<!-- Project comment -->");
        await Assert.That(csprojResult).Contains("<!-- Package references -->");
    }

    [Test]
    public async Task MigrationDoesNotModifyUnrelatedCsproj()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var directoryPackages =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="WindowsAzure.Storage" Version="9.3.3" />
              </ItemGroup>
            </Project>
            """;

        var csproj =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" />
              </ItemGroup>
            </Project>
            """;

        using var directory = new TempDirectory();
        var packagesPath = Path.Combine(directory, "Directory.Packages.props");
        var csprojPath = Path.Combine(directory, "MyProject.csproj");

        await File.WriteAllTextAsync(packagesPath, directoryPackages);
        await File.WriteAllTextAsync(csprojPath, csproj);

        var originalCsproj = await File.ReadAllTextAsync(csprojPath);

        await Updater.Update(cache, packagesPath, null);

        var csprojResult = await File.ReadAllTextAsync(csprojPath);

        // Csproj should be unchanged since it doesn't reference the migrated package
        await Assert.That(csprojResult).IsEqualTo(originalCsproj);
    }

    [Test]
    public async Task MigrationHandlesCaseInsensitivePackageNames()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var directoryPackages =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="WindowsAzure.Storage" Version="9.3.3" />
              </ItemGroup>
            </Project>
            """;

        var csproj =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="windowsazure.storage" />
              </ItemGroup>
            </Project>
            """;

        using var directory = new TempDirectory();
        var packagesPath = Path.Combine(directory, "Directory.Packages.props");
        var csprojPath = Path.Combine(directory, "MyProject.csproj");

        await File.WriteAllTextAsync(packagesPath, directoryPackages);
        await File.WriteAllTextAsync(csprojPath, csproj);

        await Updater.Update(cache, packagesPath, null);

        var csprojResult = await File.ReadAllTextAsync(csprojPath);

        // Csproj should have the new package name (case-insensitive match)
        await Assert.That(csprojResult).DoesNotContain("windowsazure.storage", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task MigrationPreservesCsprojNewlineStyle()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var directoryPackages =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="WindowsAzure.Storage" Version="9.3.3" />
              </ItemGroup>
            </Project>
            """;

        // Use Unix newlines
        var csproj = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup>\n    <PackageReference Include=\"WindowsAzure.Storage\" />\n  </ItemGroup>\n</Project>\n";

        using var directory = new TempDirectory();
        var packagesPath = Path.Combine(directory, "Directory.Packages.props");
        var csprojPath = Path.Combine(directory, "MyProject.csproj");

        await File.WriteAllTextAsync(packagesPath, directoryPackages);
        await File.WriteAllTextAsync(csprojPath, csproj);

        await Updater.Update(cache, packagesPath, null);

        var resultBytes = await File.ReadAllBytesAsync(csprojPath);
        var resultText = Encoding.UTF8.GetString(resultBytes);

        // Verify Unix newline style is preserved
        await Assert.That(resultText).Contains("\n");
        await Assert.That(resultText).DoesNotContain("\r\n");
    }

    [Test]
    public async Task NoMigrationDoesNotUpdateCsprojFiles()
    {
        using var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var directoryPackages =
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """;

        var csproj =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" />
              </ItemGroup>
            </Project>
            """;

        using var directory = new TempDirectory();
        var packagesPath = Path.Combine(directory, "Directory.Packages.props");
        var csprojPath = Path.Combine(directory, "MyProject.csproj");

        await File.WriteAllTextAsync(packagesPath, directoryPackages);
        await File.WriteAllTextAsync(csprojPath, csproj);

        var originalCsproj = await File.ReadAllTextAsync(csprojPath);

        await Updater.Update(cache, packagesPath, null);

        var csprojResult = await File.ReadAllTextAsync(csprojPath);

        // Csproj should be unchanged when no migration occurs
        await Assert.That(csprojResult).IsEqualTo(originalCsproj);
    }
}