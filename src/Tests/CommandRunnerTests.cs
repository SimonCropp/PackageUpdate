public class CommandRunnerTests
{
    string? targetDirectory;
    string? package;
    bool build;

    [Test]
    public async Task Empty()
    {
        await CommandRunner.RunCommand(Capture);
        await Assert.That(targetDirectory).IsEqualTo(Environment.CurrentDirectory);
        await Assert.That(package).IsNull();
        await Assert.That(build).IsFalse();
    }

    [Test]
    public async Task SingleUnNamedArg()
    {
        await CommandRunner.RunCommand(Capture, "dir");
        await Assert.That(targetDirectory).IsEqualTo("dir");
        await Assert.That(package).IsNull();
    }

    [Test]
    public async Task TargetDirectoryShort()
    {
        await CommandRunner.RunCommand(Capture, "-t", "dir");
        await Assert.That(targetDirectory).IsEqualTo(Path.GetFullPath("dir"));
        await Assert.That(package).IsNull();
    }

    [Test]
    public async Task TargetDirectoryLong()
    {
        await CommandRunner.RunCommand(Capture, "--target-directory", "dir");
        await Assert.That(targetDirectory).IsEqualTo(Path.GetFullPath("dir"));
        await Assert.That(package).IsNull();
    }

    [Test]
    public async Task BuildShort()
    {
        await CommandRunner.RunCommand(Capture, "-b");
        await Assert.That(targetDirectory).IsEqualTo(Environment.CurrentDirectory);
        await Assert.That(build).IsTrue();
    }

    [Test]
    public async Task BuildLong()
    {
        await CommandRunner.RunCommand(Capture, "--build");
        await Assert.That(targetDirectory).IsEqualTo(Environment.CurrentDirectory);
        await Assert.That(build).IsTrue();
    }

    [Test]
    public async Task PackageShort()
    {
        await CommandRunner.RunCommand(Capture, "-p", "packageName");
        await Assert.That(targetDirectory).IsEqualTo(Environment.CurrentDirectory);
        await Assert.That(package).IsEqualTo("packageName");
    }

    [Test]
    public async Task PackageLong()
    {
        await CommandRunner.RunCommand(Capture, "--package", "packageName");
        await Assert.That(targetDirectory).IsEqualTo(Environment.CurrentDirectory);
        await Assert.That(package).IsEqualTo("packageName");
    }

    [Test]
    public async Task All()
    {
        await CommandRunner.RunCommand(Capture, "--target-directory", "dir", "--package", "packageName", "--build");
        await Assert.That(targetDirectory).IsEqualTo(Path.GetFullPath("dir"));
        await Assert.That(package).IsEqualTo("packageName");
        await Assert.That(build).IsTrue();
    }

    Task Capture(string targetDirectory, string? package, bool build)
    {
        this.targetDirectory = targetDirectory;
        this.package = package;
        this.build = build;
        return Task.CompletedTask;
    }
}