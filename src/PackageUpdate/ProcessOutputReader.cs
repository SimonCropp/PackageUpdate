using System.Collections.Generic;
using System.Diagnostics;

public static class ProcessOutputReader
{
    public static IEnumerable<string> ReadLines(this Process process)
    {
        string line;
        var list = new List<string>();
        while ((line = process.StandardOutput.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            list.Add(line);
        }

        return list;
    }
}