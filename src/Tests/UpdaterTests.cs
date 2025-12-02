using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

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

        using var tempFile = TempFile.Create(content);

        await Updater.Update(tempFile.Path);

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

        using var tempFile = TempFile.Create(content);

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

        using var tempFile = TempFile.Create(content);

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

        using var tempFile = TempFile.Create(content);

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

        using var tempFile = TempFile.Create(content);

        await Updater.Update(tempFile.Path);

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

        using var tempFile = TempFile.Create(content);

        await Updater.Update(tempFile.Path);

        var result = await File.ReadAllTextAsync(tempFile.Path);

        // Invalid version should remain unchanged
        Assert.Contains("not-a-version", result);
    }
    static List<PackageSource> GetTestSources() =>
        [new("https://api.nuget.org/v3/index.json")];

    [Fact]
    public async Task GetLatestVersion_StableToStable()
    {
        var sources = GetTestSources();
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
        var sources = GetTestSources();
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
        var sources = GetTestSources();
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
        var sources = GetTestSources();
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
        var sources = GetTestSources();
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
        var sources = GetTestSources();
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
}