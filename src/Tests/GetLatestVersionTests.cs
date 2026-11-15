public class GetLatestVersionTests
{
    const string packageId = "PackageUpdate.GetLatestVersionTests";

    // The first source has 2.0.0, so nothing from the second can beat it. Its candidates are within
    // its cooldown, so checking either would log that it was skipped.
    [Test]
    public async Task LaterSourceCandidatesNoNewerThanTheBestAreNotChecked()
    {
        using var directory = new TempDirectory();
        var first = Path.Combine(directory, "first");
        var second = Path.Combine(directory, "second");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        LocalFeed.WritePackage(first, packageId, "2.0.0", DateTime.UtcNow.AddDays(-10));
        LocalFeed.WritePackage(second, packageId, "1.5.0");
        LocalFeed.WritePackage(second, packageId, "1.4.0");
        List<PackageSource> sources =
        [
            new(first, "first"),
            new(second, "second")
            {
                MinPublishAge = TimeSpan.FromHours(48)
            }
        ];
        var logger = new RecordingLogger();
        using var cache = new SourceCacheContext
        {
            RefreshMemoryCache = true
        };

        var latest = await Updater.GetLatestVersion(packageId, NuGetVersion.Parse("1.0.0"), sources, cache, logger);

        await Assert.That(latest!.Identity.Version).IsEqualTo(NuGetVersion.Parse("2.0.0"));
        await Assert.That(logger.Messages.Where(_ => _.Contains("from second"))).IsEmpty();
    }

    // A later source with a newer version still wins
    [Test]
    public async Task LaterSourceWithNewerVersionWins()
    {
        using var directory = new TempDirectory();
        var first = Path.Combine(directory, "first");
        var second = Path.Combine(directory, "second");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        LocalFeed.WritePackage(first, packageId, "1.5.0");
        LocalFeed.WritePackage(second, packageId, "2.0.0");
        LocalFeed.WritePackage(second, packageId, "1.4.0");
        List<PackageSource> sources = [new(first, "first"), new(second, "second")];
        using var cache = new SourceCacheContext
        {
            RefreshMemoryCache = true
        };

        var latest = await Updater.GetLatestVersion(packageId, NuGetVersion.Parse("1.0.0"), sources, cache);

        await Assert.That(latest!.Identity.Version).IsEqualTo(NuGetVersion.Parse("2.0.0"));
    }
}
