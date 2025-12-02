public static class ProcessOutputReader
{
    public static async Task<List<string>> ReadLines(this Process process)
    {
        var list = new List<string>();
        while (await process.StandardOutput.ReadLineAsync() is { } line)
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