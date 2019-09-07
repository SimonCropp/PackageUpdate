using Xunit.Abstractions;

public class TestBase:
    XunitApprovalBase
{
    public TestBase(ITestOutputHelper output) :
        base(output)
    {
    }

    static TestBase()
    {
    }
}