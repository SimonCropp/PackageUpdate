public struct ProcessResult
{
    public int? ExitCode;
    public bool HasFailed
    {
        get
        {
            return ExitCode != 0 || Killed;
        }
    }
    public bool Killed;

    public string Output;
    public string Error;
}