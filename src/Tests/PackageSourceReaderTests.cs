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
        await Assert.That(publicSource.MinPublishAge).IsEqualTo(TimeSpan.FromHours(24));
        await Assert.That(otherSource.MinPublishAge).IsEqualTo(TimeSpan.Zero);
    }

    // NuGet only accepts whole hours, and fails restore on anything else
    [Test]
    [Arguments("1.5")]
    [Arguments("-1")]
    [Arguments("abc")]
    public async Task RejectsMinPublishAgeHoursThatAreNotWholeHours(string hours)
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(temp, "nuget.config"),
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <configuration>
               <packageSources>
                 <clear />
                 <add key="public" value="https://api.nuget.org/v3/index.json" minPublishAgeHours="{hours}" />
               </packageSources>
             </configuration>
             """);

        var read = () => PackageSourceReader.Read(temp);

        await Assert.That(read).Throws<NuGetConfigurationException>();
    }
}
