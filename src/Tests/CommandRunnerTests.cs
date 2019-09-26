using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

public class CommandRunnerTests :
    XunitApprovalBase
{
    string? targetDirectory;
    string? package;

    [Fact]
    public void Empty()
    {
        CommandRunner.RunCommand(Capture);
        Assert.Equal(Environment.CurrentDirectory, targetDirectory);
        Assert.Null(package);
    }

    [Fact]
    public void SingleUnNamedArg()
    {
        CommandRunner.RunCommand(Capture, "dir");
        Assert.Equal("dir", targetDirectory);
        Assert.Null(package);
    }

    [Fact]
    public void TargetDirectoryShort()
    {
        CommandRunner.RunCommand(Capture, "-t", "dir");
        Assert.Equal(Path.GetFullPath("dir"), targetDirectory);
        Assert.Null(package);
    }

    [Fact]
    public void TargetDirectoryLong()
    {
        CommandRunner.RunCommand(Capture, "--target-directory", "dir");
        Assert.Equal(Path.GetFullPath("dir"), targetDirectory);
        Assert.Null(package);
    }

    [Fact]
    public void PackageShort()
    {
        CommandRunner.RunCommand(Capture, "-p", "packageName");
        Assert.Equal(Environment.CurrentDirectory, targetDirectory);
        Assert.Equal("packageName", package);
    }

    [Fact]
    public void PackageLong()
    {
        CommandRunner.RunCommand(Capture, "--package", "packageName");
        Assert.Equal(Environment.CurrentDirectory, targetDirectory);
        Assert.Equal("packageName", package);
    }

    [Fact]
    public void All()
    {
        CommandRunner.RunCommand(Capture, "--target-directory", "dir", "--package", "packageName");
        Assert.Equal(Path.GetFullPath("dir"), targetDirectory);
        Assert.Equal("packageName", package);
    }

    void Capture(string targetDirectory, string? package)
    {
        this.targetDirectory = targetDirectory;
        this.package = package;
    }

    public CommandRunnerTests(ITestOutputHelper output) :
        base(output)
    {
    }
}