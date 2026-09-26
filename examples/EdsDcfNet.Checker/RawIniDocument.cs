namespace EdsDcfNet.Checker;

/// <summary>A key/value entry together with the line it was read from.</summary>
public sealed record RawEntry(string Key, string Value, int Line);

/// <summary>An INI section together with the line of its header.</summary>
public sealed class RawSection
{
    public RawSection(string name, int line)
    {
        Name = name;
        Line = line;
    }

    public string Name { get; }

    public int Line { get; }

    public Dictionary<string, RawEntry> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);

    public RawEntry? Get(string key) => Entries.TryGetValue(key, out var entry) ? entry : null;

    /// <summary>Returns the trimmed value, or <see langword="null"/> when the key is absent or empty.</summary>
    public string? GetValue(string key)
    {
        var entry = Get(key);
        return entry is null || string.IsNullOrWhiteSpace(entry.Value) ? null : entry.Value.Trim();
    }
}

/// <summary>
/// Line-aware INI scan of an EDS/DCF file. Unlike the library reader it never throws on bad
/// values, so every problem in the file can be collected and reported with its line number.
/// </summary>
public sealed class RawIniDocument
{
    private RawIniDocument()
    {
    }

    public Dictionary<string, RawSection> Sections { get; } = new(StringComparer.OrdinalIgnoreCase);

    public RawSection? Get(string name) => Sections.TryGetValue(name, out var section) ? section : null;

    public static RawIniDocument Parse(string filePath, List<Finding> findings)
    {
        var document = new RawIniDocument();
        RawSection? current = null;
        var lineNumber = 0;

        foreach (var rawLine in File.ReadLines(filePath))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#')
            {
                continue;
            }

            if (line[0] == '[')
            {
                if (line[^1] != ']' || line.Length < 3)
                {
                    findings.Add(new Finding(Severity.Error, "INI001", filePath, lineNumber, null, null, null,
                        "Malformed section header '" + line + "'."));
                    current = null;
                    continue;
                }

                var name = line[1..^1].Trim();
                if (document.Sections.TryGetValue(name, out var existing))
                {
                    findings.Add(new Finding(Severity.Error, "INI002", filePath, lineNumber, name, null, null,
                        "Duplicate section; first defined on line " + existing.Line.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                        ". Readers merge or drop duplicate keys unpredictably."));
                    current = existing;
                    continue;
                }

                current = new RawSection(name, lineNumber);
                document.Sections.Add(name, current);
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                findings.Add(new Finding(Severity.Error, "INI001", filePath, lineNumber, current?.Name, null, null,
                    "Line is neither a section header nor a 'Key=Value' entry: '" + line + "'."));
                continue;
            }

            if (current is null)
            {
                findings.Add(new Finding(Severity.Error, "INI004", filePath, lineNumber, null, null, null,
                    "Entry outside of any section: '" + line + "'."));
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (current.Entries.TryGetValue(key, out var previous))
            {
                findings.Add(new Finding(Severity.Warning, "INI003", filePath, lineNumber, current.Name, key, value,
                    "Duplicate key; previous value '" + previous.Value + "' on line " +
                    previous.Line.ToString(System.Globalization.CultureInfo.InvariantCulture) + " is overridden."));
            }

            current.Entries[key] = new RawEntry(key, value, lineNumber);
        }

        return document;
    }
}
