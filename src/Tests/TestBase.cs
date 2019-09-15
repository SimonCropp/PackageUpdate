using Xunit.Abstractions;

public class TestBase:
    XunitApprovalBase
{
    public TestBase(ITestOutputHelper output, [CallerFilePath] string sourceFilePath = "") :
        base(output, sourceFilePath)
    {
    }

    static TestBase()
    {
    }
}