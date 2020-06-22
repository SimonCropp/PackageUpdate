using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

public class CommandRunnerTests
{
    string? targetDirectory;
    string? package;

    [Fact]
    public async Task Empty()
    {
        await CommandRunner.RunCommand(Capture);
        Assert.Equal(Environment.CurrentDirectory, targetDirectory);
        Assert.Null(package);
    }

    [Fact]
    public async Task SingleUnNamedArg()
    {
        await CommandRunner.RunCommand(Capture, "dir");
        Assert.Equal("dir", targetDirectory);
        Assert.Null(package);
    }

    [Fact]
    public async Task TargetDirectoryShort()
    {
        await CommandRunner.RunCommand(Capture, "-t", "dir");
        Assert.Equal(Path.GetFullPath("dir"), targetDirectory);
        Assert.Null(package);
    }

    [Fact]
    public async Task TargetDirectoryLong()
    {
        await CommandRunner.RunCommand(Capture, "--target-directory", "dir");
        Assert.Equal(Path.GetFullPath("dir"), targetDirectory);
        Assert.Null(package);
    }

    [Fact]
    public async Task PackageShort()
    {
        await CommandRunner.RunCommand(Capture, "-p", "packageName");
        Assert.Equal(Environment.CurrentDirectory, targetDirectory);
        Assert.Equal("packageName", package);
    }

    [Fact]
    public async Task PackageLong()
    {
        await CommandRunner.RunCommand(Capture, "--package", "packageName");
        Assert.Equal(Environment.CurrentDirectory, targetDirectory);
        Assert.Equal("packageName", package);
    }

    [Fact]
    public async Task All()
    {
        await CommandRunner.RunCommand(Capture, "--target-directory", "dir", "--package", "packageName");
        Assert.Equal(Path.GetFullPath("dir"), targetDirectory);
        Assert.Equal("packageName", package);
    }

    Task Capture(string targetDirectory, string? package)
    {
        this.targetDirectory = targetDirectory;
        this.package = package;
        return Task.CompletedTask;
    }
}