public class Options
{
    [Option('t', "target-directory", Required = false)]
    public string? TargetDirectory { get; set; }
    [Option('b', "build", Required = false)]
    public bool Build { get; set; }

    [Option('p', "package", Required = false)]
    public string? Package { get; set; }
}