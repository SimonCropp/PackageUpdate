/// <summary>
/// Loads an xml file and applies attribute updates as text splices against the original bytes.
/// Rewriting via <see cref="XmlWriter"/> normalizes whitespace, which collapses elements that
/// spread their attributes over multiple lines. Splicing preserves the file byte for byte
/// apart from the values that actually changed.
/// </summary>
class XmlEditor
{
    string text;
    List<int> lineStarts;
    List<(int Start, int Length, string Replacement)> edits = [];
    bool hasBom;

    public XDocument Document { get; }

    XmlEditor(string text, bool hasBom, XDocument document)
    {
        this.text = text;
        this.hasBom = hasBom;
        Document = document;
        lineStarts = BuildLineStarts(text);
    }

    public static XmlEditor Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hasBom = bytes is [0xEF, 0xBB, 0xBF, ..];
        var text = new UTF8Encoding(false).GetString(hasBom ? bytes.AsSpan(3) : bytes);
        var document = XDocument.Parse(text, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        return new(text, hasBom, document);
    }

    public bool HasEdits => edits.Count > 0;

    public void SetAttribute(XElement element, string name, string value)
    {
        // Keep the in memory document in sync so subsequent reads see the new value
        element.SetAttributeValue(name, value);

        var tag = FindStartTag(element);
        var match = Regex.Match(
            text[tag.Start..tag.End],
            $"""(?<=[\s'"/>])(?<name>{Regex.Escape(name)})\s*=\s*(?<quote>["'])(?<value>[^"']*)\k<quote>""");

        var encoded = Encode(value);
        if (match.Success)
        {
            var group = match.Groups["value"];
            edits.Add((tag.Start + group.Index, group.Length, encoded));
            return;
        }

        // Attribute does not exist yet: append it directly after the element name
        var nameEnd = tag.Start;
        while (nameEnd < tag.End &&
               !char.IsWhiteSpace(text[nameEnd]) &&
               text[nameEnd] != '/' &&
               text[nameEnd] != '>')
        {
            nameEnd++;
        }

        edits.Add((nameEnd, 0, $" {name}=\"{encoded}\""));
    }

    public Task Save(string path)
    {
        if (!HasEdits)
        {
            return Task.CompletedTask;
        }

        var builder = new StringBuilder(text);
        foreach (var (start, length, replacement) in edits.OrderByDescending(_ => _.Start))
        {
            builder.Remove(start, length);
            builder.Insert(start, replacement);
        }

        text = builder.ToString();
        edits.Clear();
        lineStarts = BuildLineStarts(text);

        var encoding = new UTF8Encoding(hasBom);
        return File.WriteAllTextAsync(path, text, encoding);
    }

    (int Start, int End) FindStartTag(XElement element)
    {
        var info = (IXmlLineInfo) element;
        if (!info.HasLineInfo())
        {
            throw new("Element has no line info");
        }

        // LinePosition points at the first character of the element name, ie just past the '<'
        var start = lineStarts[info.LineNumber - 1] + info.LinePosition - 1;

        var index = start;
        char? quote = null;
        while (index < text.Length)
        {
            var current = text[index];
            if (quote != null)
            {
                if (current == quote)
                {
                    quote = null;
                }
            }
            else if (current is '"' or '\'')
            {
                quote = current;
            }
            else if (current == '>')
            {
                return (start, index);
            }

            index++;
        }

        throw new($"Could not find the end of the start tag for {element.Name}");
    }

    static List<int> BuildLineStarts(string text)
    {
        var starts = new List<int> {0};
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
            else if (text[i] == '\r')
            {
                if (i + 1 < text.Length &&
                    text[i + 1] == '\n')
                {
                    i++;
                }

                starts.Add(i + 1);
            }
        }

        return starts;
    }

    static string Encode(string value) =>
        value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
}
