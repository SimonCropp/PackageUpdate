using System.Linq;
using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

[UsesVerify]
public class PendingUpdateReaderTests
{
    [Fact]
    public Task Deprecated()
    {
        var input = @"
The following sources were used:
   https://api.nuget.org/v3/index.json
   C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\

Project `Tests` has the following updates to its packages
   [net472]:
   Top-level Package      Requested   Resolved   Latest
   > HtmlAgilityPack      1.11.6      1.11.6     1.11.7 (D)

";
        return VerifyUpdates(input);
    }

    [Fact]
    public Task Simple()
    {
        var input = @"
The following sources were used:
   https://api.nuget.org/v3/index.json
   C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\

Project `Tests` has the following updates to its packages
   [net472]:
   Top-level Package      Requested   Resolved   Latest
   > HtmlAgilityPack      1.11.6      1.11.6     1.11.7

";
        return VerifyUpdates(input);
    }

    [Fact]
    public Task PreRelease()
    {
        var input = @"
The following sources were used:
   https://api.nuget.org/v3/index.json
   C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\

Project `Tests` has the following updates to its packages
   [netcoreapp3.1]:
   Top-level Package             Requested       Resolved        Latest
   > Microsoft.NET.Test.Sdk      16.4.0          16.4.0          16.5.0-preview-20191115-01
   > Verify.Xunit                1.0.0-beta.31   1.0.0-beta.31   1.0.0-beta.32

";
        return VerifyUpdates(input);
    }

    static Task VerifyUpdates(string input)
    {
        var lines = input.Lines().ToList();
        return Verifier.Verify(
            new
            {
                parsed = PendingUpdateReader.ParseUpdates(lines),
                withUpdates = PendingUpdateReader.ParseWithUpdates(lines)
            });
    }
}