static class Extensions
{
    public static IEnumerable<string> Lines(this string target)
    {
        using var reader = new StringReader(target);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}