static class ToolManifest
{
    static byte[] bom = [0xEF, 0xBB, 0xBF];

    static JsonReaderOptions options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static List<ToolEntry> Read(byte[] bytes)
    {
        // Utf8JsonReader does not skip a byte order mark
        var offset = bytes.AsSpan().StartsWith(bom) ? bom.Length : 0;
        var reader = new Utf8JsonReader(bytes.AsSpan(offset), options);

        var entries = new List<ToolEntry>();
        var inTools = false;
        string? tool = null;
        string? property = null;
        string? version = null;
        var versionStart = 0;
        var versionLength = 0;
        var pinned = false;

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    var name = reader.GetString()!;
                    switch (reader.CurrentDepth)
                    {
                        case 1:
                            inTools = name == "tools";
                            break;
                        case 2 when inTools:
                            tool = name;
                            version = null;
                            pinned = false;
                            break;
                        case 3 when inTools:
                            property = name;
                            break;
                    }

                    break;

                case JsonTokenType.String
                    when inTools && reader.CurrentDepth == 3 && property == "version":
                    version = reader.GetString();
                    // TokenStartIndex is the opening quote, so step over it to get the raw value
                    versionStart = offset + (int) reader.TokenStartIndex + 1;
                    versionLength = reader.ValueSpan.Length;
                    break;

                case JsonTokenType.True
                    when inTools && reader.CurrentDepth == 3 && property == "pinned":
                    pinned = true;
                    break;

                // End of a single tool
                case JsonTokenType.EndObject when inTools && reader.CurrentDepth == 2:
                    if (tool != null &&
                        version != null)
                    {
                        entries.Add(new(tool, version, versionStart, versionLength, pinned));
                    }

                    tool = null;
                    break;
            }
        }

        return entries;
    }

    public static byte[] ApplyUpdates(byte[] bytes, List<(ToolEntry Tool, NuGetVersion Version)> updates)
    {
        var result = new List<byte>(bytes);

        // Splice from the end of the file so the offsets of earlier tools stay valid
        foreach (var (tool, version) in updates.OrderByDescending(_ => _.Tool.VersionStart))
        {
            result.RemoveRange(tool.VersionStart, tool.VersionLength);
            result.InsertRange(tool.VersionStart, Encoding.UTF8.GetBytes(version.ToString()));
        }

        return [..result];
    }
}
