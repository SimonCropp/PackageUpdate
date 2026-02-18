public class PackageCacheKeyComparerTests
{
    static PackageCacheKeyComparer comparer = PackageCacheKeyComparer.Instance;

    [Test]
    public async Task Equals_SamePackageAndVersion()
    {
        var x = ("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"));
        var y = ("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"));

        await Assert.That(comparer.Equals(x, y)).IsTrue();
    }

    [Test]
    public async Task Equals_DifferentCase()
    {
        var x = ("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"));
        var y = ("newtonsoft.json", NuGetVersion.Parse("13.0.1"));

        await Assert.That(comparer.Equals(x, y)).IsTrue();
    }

    [Test]
    public async Task Equals_DifferentVersion()
    {
        var x = ("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"));
        var y = ("Newtonsoft.Json", NuGetVersion.Parse("12.0.1"));

        await Assert.That(comparer.Equals(x, y)).IsFalse();
    }

    [Test]
    public async Task Equals_DifferentPackage()
    {
        var x = ("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"));
        var y = ("NUnit", NuGetVersion.Parse("13.0.1"));

        await Assert.That(comparer.Equals(x, y)).IsFalse();
    }

    [Test]
    public async Task GetHashCode_SameForDifferentCase()
    {
        var x = ("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"));
        var y = ("newtonsoft.json", NuGetVersion.Parse("13.0.1"));

        await Assert.That(comparer.GetHashCode(x)).IsEqualTo(comparer.GetHashCode(y));
    }

    [Test]
    public async Task GetHashCode_DifferentForDifferentVersion()
    {
        var x = ("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"));
        var y = ("Newtonsoft.Json", NuGetVersion.Parse("12.0.1"));

        await Assert.That(comparer.GetHashCode(x)).IsNotEqualTo(comparer.GetHashCode(y));
    }

    [Test]
    public async Task GetHashCode_DifferentForDifferentPackage()
    {
        var x = ("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"));
        var y = ("NUnit", NuGetVersion.Parse("13.0.1"));

        await Assert.That(comparer.GetHashCode(x)).IsNotEqualTo(comparer.GetHashCode(y));
    }

    [Test]
    public async Task WorksWithDictionary()
    {
        var dictionary = new Dictionary<(string Package, NuGetVersion Version), string>(comparer)
        {
            [("Newtonsoft.Json", NuGetVersion.Parse("13.0.1"))] = "first"
        };

        await Assert.That(dictionary.ContainsKey(("newtonsoft.json", NuGetVersion.Parse("13.0.1")))).IsTrue();
        await Assert.That(dictionary.ContainsKey(("NEWTONSOFT.JSON", NuGetVersion.Parse("13.0.1")))).IsTrue();
        await Assert.That(dictionary.ContainsKey(("Newtonsoft.Json", NuGetVersion.Parse("12.0.1")))).IsFalse();
    }
}