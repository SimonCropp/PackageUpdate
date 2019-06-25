using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

public class CommandRunnerTests :
    XunitLoggingBase
{
    string targetDirectory;

    [Fact]
    public void Empty()
    {
        CommandRunner.RunCommand(Capture);
        Assert.Equal(Environment.CurrentDirectory, targetDirectory);
    }

    [Fact]
    public void SingleUnNamedArg()
    {
        CommandRunner.RunCommand(Capture, "dir");
        Assert.Equal("dir", targetDirectory);
    }

    [Fact]
    public void TargetDirectoryShort()
    {
        CommandRunner.RunCommand(Capture, "-t", "dir");
        Assert.Equal(Path.GetFullPath("dir"), targetDirectory);
    }

    [Fact]
    public void TargetDirectoryLong()
    {
        CommandRunner.RunCommand(Capture, "--target-directory", "dir");
        Assert.Equal(Path.GetFullPath("dir"), targetDirectory);
    }

    void Capture(string targetDirectory)
    {
        this.targetDirectory = targetDirectory;
    }

    public CommandRunnerTests(ITestOutputHelper output) :
        base(output)
    {
    }
}