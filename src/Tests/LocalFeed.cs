using System.IO.Compression;

// Writes packages into a local folder feed. A local feed gives a version the last write time of its
// nupkg as its publish date, so a test can make a version as old as it needs.
static class LocalFeed
{
    public static void WritePackage(string feed, string id, string version, DateTime? published = null)
    {
        var path = Path.Combine(feed, $"{id}.{version}.nupkg");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry($"{id}.nuspec").Open()))
        {
            writer.Write(
                $"""
                 <?xml version="1.0" encoding="utf-8"?>
                 <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
                   <metadata>
                     <id>{id}</id>
                     <version>{version}</version>
                     <authors>PackageUpdate</authors>
                     <description>Test package</description>
                   </metadata>
                 </package>
                 """);
        }

        if (published != null)
        {
            File.SetLastWriteTimeUtc(path, published.Value);
        }
    }
}
