public class PackageCacheKeyComparerTests
{
    static PackageCacheKeyComparer comparer = PackageCacheKeyComparer.Instance;

    [Fact]
    public void Equals_SamePackageAndVersion()
    {
        var x = ("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"));
        var y = ("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"));

        Assert.True(comparer.Equals(x, y));
    }

    [Fact]
    public void Equals_DifferentCase()
    {
        var x = ("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"));
        var y = ("newtonsoft.json", NuGetVersion.Parse("13.0.1"));

        Assert.True(comparer.Equals(x, y));
    }

    [Fact]
    public void Equals_DifferentVersion()
    {
        var x = ("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"));
        var y = ("Newtonsoft.Json", NuGetVersion.Parse("12.0.1"));

        Assert.False(comparer.Equals(x, y));
    }

    [Fact]
    public void Equals_DifferentPackage()
    {
        var x = ("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"));
        var y = ("NUnit", NuGetVersion.Parse("13.0.1"));

        Assert.False(comparer.Equals(x, y));
    }

    [Fact]
    public void GetHashCode_SameForDifferentCase()
    {
        var x = ("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"));
        var y = ("newtonsoft.json", NuGetVersion.Parse("13.0.1"));

        Assert.Equal(comparer.GetHashCode(x), comparer.GetHashCode(y));
    }

    [Fact]
    public void GetHashCode_DifferentForDifferentVersion()
    {
        var x = ("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"));
        var y = ("Newtonsoft.Json", NuGetVersion.Parse("12.0.1"));

        Assert.NotEqual(comparer.GetHashCode(x), comparer.GetHashCode(y));
    }

    [Fact]
    public void GetHashCode_DifferentForDifferentPackage()
    {
        var x = ("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"));
        var y = ("NUnit", NuGetVersion.Parse("13.0.1"));

        Assert.NotEqual(comparer.GetHashCode(x), comparer.GetHashCode(y));
    }

    [Fact]
    public void WorksWithDictionary()
    {
        Dictionary<(string Package, NuGetVersion Version), string> dictionary = new(comparer);

        dictionary[("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"))] = "first";

        Assert.True(dictionary.ContainsKey(("newtonsoft.json", NuGetVersion.Parse("13.0.1"))));
        Assert.True(dictionary.ContainsKey(("NEWTONSOFT.JSON", NuGetVersion.Parse("13.0.1"))));
        Assert.False(dictionary.ContainsKey(("Newtonsoft.Json", NuGetVersion.Parse("12.0.1"))));
    }
}
