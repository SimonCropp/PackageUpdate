﻿public class PendingUpdateReaderTests
{
    [Fact]
    public Task Deprecated()
    {
        var input = """
                    The following sources were used:
                       https://api.nuget.org/v3/index.json
                       C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\

                    Project `Tests` has the following updates to its packages
                       [net472]:
                       Top-level Package      Requested   Resolved   Latest
                       > HtmlAgilityPack      1.11.6      1.11.6     1.11.7 (D)
                    """;
        return VerifyUpdates(input);
    }

    [Fact]
    public Task Not_found_at_the_sources()
    {
        var input = """
                    The following sources were used:
                       https://api.nuget.org/v3/index.json

                    Project `Tests` has the following updates to its packages
                       [net8.0]:
                       Top-level Package      Requested      Resolved       Latest
                       > xunit.v3             0.3.0-pre.18   0.4.0-pre.20   Not found at the sources
                    """;
        return VerifyUpdates(input);
    }

    [Fact]
    public Task Simple()
    {
        var input = """
                    The following sources were used:
                       https://api.nuget.org/v3/index.json
                       C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\

                    Project `Tests` has the following updates to its packages
                       [net472]:
                       Top-level Package      Requested   Resolved   Latest
                       > HtmlAgilityPack      1.11.6      1.11.6     1.11.7
                    """;
        return VerifyUpdates(input);
    }

    [Fact]
    public Task SameVersion()
    {
        var input = """
                    The following sources were used:
                       https://api.nuget.org/v3/index.json
                       C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\

                    Project `Tests` has the following updates to its packages
                       [net472]:
                       Top-level Package      Requested   Resolved   Latest
                       > HtmlAgilityPack      1.11.6      1.11.6     1.11.6
                    """;
        return VerifyUpdates(input);
    }

    [Fact]
    public Task PreRelease()
    {
        var input = """
                    The following sources were used:
                       https://api.nuget.org/v3/index.json
                       C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\

                    Project `Tests` has the following updates to its packages
                       [netcoreapp3.1]:
                       Top-level Package             Requested       Resolved        Latest
                       > Microsoft.NET.Test.Sdk      16.4.0          16.4.0          16.5.0-preview-20191115-01
                       > Verify.Xunit                1.0.0-beta.31   1.0.0-beta.31   1.0.0-beta.32
                    """;
        return VerifyUpdates(input);
    }

    static Task VerifyUpdates(string input)
    {
        var lines = input.Lines().ToList();
        return Verify(
            new
            {
                parsed = PendingUpdateReader.ParseUpdates(lines),
                withUpdates = PendingUpdateReader.ParseWithUpdates(lines)
            });
    }
}