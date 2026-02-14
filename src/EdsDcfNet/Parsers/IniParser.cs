namespace EdsDcfNet.Parsers;

using EdsDcfNet.Exceptions;

/// <summary>
/// Low-level INI file parser for EDS/DCF files.
/// Parses INI-style files with sections and key-value pairs.
/// </summary>
public class IniParser
{
    /// <summary>
    /// Parses an EDS/DCF file and returns sections with their key-value pairs.
    /// </summary>
    /// <param name="filePath">Path to the EDS/DCF file</param>
    /// <returns>Dictionary where key is section name (lowercase) and value is key-value pairs</returns>
    public Dictionary<string, Dictionary<string, string>> ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"EDS/DCF file not found: {filePath}");

        var lines = File.ReadAllLines(filePath);
        return ParseLines(lines);
    }

    /// <summary>
    /// Parses EDS/DCF content from a string.
    /// </summary>
    /// <param name="content">EDS/DCF file content as string</param>
    /// <returns>Dictionary where key is section name (lowercase) and value is key-value pairs</returns>
    public Dictionary<string, Dictionary<string, string>> ParseString(string content)
    {
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return ParseLines(lines);
    }

    /// <summary>
    /// Parses lines of an EDS/DCF file.
    /// </summary>
    private Dictionary<string, Dictionary<string, string>> ParseLines(string[] lines)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;
        var lineNumber = 0;

        foreach (var rawLine in lines)
        {
            lineNumber++;
            var line = rawLine.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";"))
                continue;

            // Check for section header
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentSection = line.Substring(1, line.Length - 2).Trim();

                if (!sections.ContainsKey(currentSection))
                {
                    sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                continue;
            }

            // Parse key-value pair
            var equalIndex = line.IndexOf('=');
            if (equalIndex > 0)
            {
                if (currentSection == null)
                {
                    throw new EdsParseException($"Key-value pair found outside of any section at line {lineNumber}", lineNumber);
                }

                var key = line.Substring(0, equalIndex).Trim();
                var value = equalIndex < line.Length - 1
                    ? line.Substring(equalIndex + 1).Trim()
                    : string.Empty;

                sections[currentSection][key] = value;
            }
        }

        return sections;
    }

    /// <summary>
    /// Gets a value from a section, or returns default value if not found.
    /// </summary>
    /// <param name="sections">Parsed sections dictionary</param>
    /// <param name="sectionName">Name of the section</param>
    /// <param name="key">Key to retrieve</param>
    /// <param name="defaultValue">Default value if key not found</param>
    /// <returns>Value from the section or default value</returns>
    public static string GetValue(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string key,
        string defaultValue = "")
    {
        if (sections.TryGetValue(sectionName, out var section))
        {
            if (section.TryGetValue(key, out var value))
            {
                return value;
            }
        }
        return defaultValue;
    }

    /// <summary>
    /// Checks if a section exists.
    /// </summary>
    /// <param name="sections">Parsed sections dictionary</param>
    /// <param name="sectionName">Name of the section to check</param>
    /// <returns>True if section exists, false otherwise</returns>
    public static bool HasSection(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName)
    {
        return sections.ContainsKey(sectionName);
    }

    /// <summary>
    /// Gets all keys from a section.
    /// </summary>
    /// <param name="sections">Parsed sections dictionary</param>
    /// <param name="sectionName">Name of the section</param>
    /// <returns>Enumerable of all keys in the section, or empty if section not found</returns>
    public static IEnumerable<string> GetKeys(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName)
    {
        if (sections.TryGetValue(sectionName, out var section))
        {
            return section.Keys;
        }
        return Enumerable.Empty<string>();
    }
}
