using System.Collections.Generic;
using System.IO;

static class Extensions
{
    public static IEnumerable<string> Lines(this string target)
    {
        using var reader = new StringReader(target);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            yield return line;
        }
    }
}