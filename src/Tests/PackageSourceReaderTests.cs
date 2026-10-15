public class PackageSourceReaderTests
{
    [Test]
    public async Task ReadsMinPublishAgeHours()
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(temp, "nuget.config"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="public" value="https://api.nuget.org/v3/index.json" minPublishAgeHours="24" />
                <add key="other" value="https://example.com/v3/index.json" />
              </packageSources>
            </configuration>
            """);

        var sources = PackageSourceReader.Read(temp);

        var publicSource = sources.Single(_ => _.Name == "public");
        var otherSource = sources.Single(_ => _.Name == "other");
        await Assert.That(PackageSourceReader.GetMinPublishAge(publicSource)).IsEqualTo(TimeSpan.FromHours(24));
        await Assert.That(PackageSourceReader.GetMinPublishAge(otherSource)).IsNull();
    }

    [Test]
    public async Task GetLatestVersion_SkipsVersionsWithinMinPublishAge()
    {
        using var cache = new SourceCacheContext
        {
            RefreshMemoryCache = true
        };
        var source = new PackageSource("https://api.nuget.org/v3/index.json");
        // Larger than the age of any published version
        PackageSourceReader.SetMinPublishAge(source, TimeSpan.FromDays(365 * 100));

        var result = await Updater.GetLatestVersion(
            "Newtonsoft.Json",
            NuGetVersion.Parse("12.0.2"),
            [source],
            cache);

        await Assert.That(result).IsNull();
    }
}
