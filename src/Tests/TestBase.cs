using Xunit.Abstractions;

public class TestBase:
    XunitLoggingBase
{
    public TestBase(ITestOutputHelper output) :
        base(output)
    {
    }

    static TestBase()
    {
    }
}