using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

public static class ProcessOutputReader
{
    public static async Task<List<string>> ReadLines(this Process process)
    {
        string? line;
        var list = new List<string>();
        while ((line = await process.StandardOutput.ReadLineAsync()) != null)
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