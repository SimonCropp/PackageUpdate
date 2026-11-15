using System.IO.Compression;

public class LookupCacheTests
{
    const string packageId = "PackageUpdate.LookupCacheTests";

    [Test]
    public async Task GetLatestVersion_KeptApartPerSources()
    {
        using var cache = new SourceCacheContext
        {
            RefreshMemoryCache = true
        };
        var source = new PackageSource("https://api.nuget.org/v3/index.json");
        // The same feed, as a nuget.config with a cooldown longer than the age of any published version would give it
        var withCooldown = new PackageSource("https://api.nuget.org/v3/index.json");
        PackageSourceReader.SetMinPublishAge(withCooldown, TimeSpan.FromDays(365 * 100));

        var latest = await Updater.GetLatestVersion(
            "Newtonsoft.Json",
            NuGetVersion.Parse("12.0.1"),
            [source],
            cache);
        var latestWithCooldown = await Updater.GetLatestVersion(
            "Newtonsoft.Json",
            NuGetVersion.Parse("12.0.1"),
            [withCooldown],
            cache);

        await Assert.That(latest).IsNotNull();
        await Assert.That(latestWithCooldown).IsNull();
    }

    [Test]
    public async Task GetLatestVersion_KeptForTheLifeOfTheCache()
    {
        using var feed = new TempDirectory();
        WritePackage(feed, "1.0.0");
        WritePackage(feed, "1.1.0");
        var source = new PackageSource(feed);
        var current = NuGetVersion.Parse("1.0.0");

        using var firstCache = new SourceCacheContext
        {
            RefreshMemoryCache = true
        };
        var before = await Updater.GetLatestVersion(packageId, current, [source], firstCache);

        WritePackage(feed, "1.2.0");
        var sameCache = await Updater.GetLatestVersion(packageId, current, [source], firstCache);

        // A new cache, as each PackageUpdater.Run makes, sees the version published since
        using var secondCache = new SourceCacheContext
        {
            RefreshMemoryCache = true
        };
        var newCache = await Updater.GetLatestVersion(packageId, current, [source], secondCache);

        await Assert.That(before!.Identity.Version).IsEqualTo(NuGetVersion.Parse("1.1.0"));
        await Assert.That(sameCache!.Identity.Version).IsEqualTo(NuGetVersion.Parse("1.1.0"));
        await Assert.That(newCache!.Identity.Version).IsEqualTo(NuGetVersion.Parse("1.2.0"));
    }

    static void WritePackage(string feed, string version)
    {
        using var archive = ZipFile.Open(Path.Combine(feed, $"{packageId}.{version}.nupkg"), ZipArchiveMode.Create);
        using var writer = new StreamWriter(archive.CreateEntry($"{packageId}.nuspec").Open());
        writer.Write(
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
               <metadata>
                 <id>{packageId}</id>
                 <version>{version}</version>
                 <authors>PackageUpdate</authors>
                 <description>Test package</description>
               </metadata>
             </package>
             """);
    }
}
