public class MinPublishAgeTests
{
    // Under a 48 hour cooldown, 2.0.0 is too new, so 1.1.0 is the newest that can be selected.
    // minPublishAgeExceptions exempts Contoso.*, so it moves to 2.0.0.
    [Test]
    public async Task Update_SelectsNewestOldEnoughVersion_UnlessExempt()
    {
        using var directory = new TempDirectory();
        var feed = Path.Combine(directory, "feed");
        Directory.CreateDirectory(feed);
        var tenDaysAgo = DateTime.UtcNow.AddDays(-10);
        foreach (var id in new[] {"Contoso.Exempt", "Other.Package"})
        {
            LocalFeed.WritePackage(feed, id, "1.0.0", tenDaysAgo);
            LocalFeed.WritePackage(feed, id, "1.1.0", tenDaysAgo);
            LocalFeed.WritePackage(feed, id, "2.0.0");
        }

        await File.WriteAllTextAsync(
            Path.Combine(directory, "nuget.config"),
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <configuration>
               <packageSources>
                 <clear />
                 <add key="local" value="{feed}" minPublishAgeHours="48" />
               </packageSources>
               <minPublishAgeExceptions>
                 <package pattern="Contoso.*" />
               </minPublishAgeExceptions>
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
                <PackageVersion Include="Contoso.Exempt" Version="1.0.0" />
                <PackageVersion Include="Other.Package" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        using var cache = new SourceCacheContext
        {
            RefreshMemoryCache = true
        };
        var result = await Updater.Update(cache, props, null);

        var versions = result.Changes.ToDictionary(_ => _.Package, _ => _.To.ToString());
        await Assert.That(versions["Contoso.Exempt"]).IsEqualTo("2.0.0");
        await Assert.That(versions["Other.Package"]).IsEqualTo("1.1.0");
    }

    // NuGet rejects a value that is not whole hours, so the solution fails, the same as restore would
    [Test]
    public async Task Run_ReportsMinPublishAgeHoursThatAreNotWholeHours()
    {
        using var directory = new TempDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(directory, "nuget.config"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" minPublishAgeHours="1.5" />
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
              </ItemGroup>
            </Project>
            """);

        var result = await PackageUpdater.Run(directory, null, false);

        var problem = result.Problems.Single();
        await Assert.That(problem.Kind).IsEqualTo(UpdateProblemKind.Failed);
        await Assert.That(problem.Message).Contains("minPublishAgeHours");
        await Assert.That(result.Succeeded).IsFalse();
    }

    static DateTimeOffset now = new(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task CooldownSkipReason_NoMinPublishAge() =>
        await Assert.That(Updater.CooldownSkipReason(null, TimeSpan.Zero, now)).IsNull();

    [Test]
    public async Task CooldownSkipReason_OldEnough() =>
        await Assert.That(Updater.CooldownSkipReason(now.AddHours(-48), TimeSpan.FromHours(48), now)).IsNull();

    [Test]
    public async Task CooldownSkipReason_TooNew() =>
        await Assert.That(Updater.CooldownSkipReason(now.AddHours(-47), TimeSpan.FromHours(48), now))
            .IsEqualTo("published within minPublishAgeHours");

    // A version whose age cannot be known is never assumed to be old enough
    [Test]
    public async Task CooldownSkipReason_NoPublishDate() =>
        await Assert.That(Updater.CooldownSkipReason(null, TimeSpan.FromHours(48), now))
            .IsEqualTo("no publish date to check against minPublishAgeHours");
}
