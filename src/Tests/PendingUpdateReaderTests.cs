using System.Threading.Tasks;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

public class PendingUpdateReaderTests :
    VerifyBase
{
    [Fact]
    public Task Simple()
    {
        var lines = @"
The following sources were used:
   https://api.nuget.org/v3/index.json
   C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\

Project `Tests` has the following updates to its packages
   [net472]:
   Top-level Package      Requested   Resolved   Latest
   > HtmlAgilityPack      1.11.6      1.11.6     1.11.7

".Lines();
        return Verify(PendingUpdateReader.ParseUpdates(lines));
    }

    public PendingUpdateReaderTests(ITestOutputHelper output) :
        base(output)
    {
    }
}