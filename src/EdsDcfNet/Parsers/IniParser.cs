namespace EdsDcfNet.Parsers;

using System.Collections.Generic;
using System.Globalization;
using EdsDcfNet.Exceptions;

/// <summary>
/// Low-level INI file parser for EDS/DCF files.
/// Parses INI-style files with sections and key-value pairs.
/// </summary>
public class IniParser
{
    /// <summary>
    /// Default maximum input size (10 MB) used by <see cref="ParseFile"/> and
    /// <see cref="ParseString"/> to guard against unbounded memory consumption.
    /// </summary>
    public const long DefaultMaxInputSize = 10L * 1024 * 1024;

    private readonly long _maxInputSize;

    /// <summary>
    /// Initializes a new instance of <see cref="IniParser"/> with the default size limit.
    /// </summary>
    public IniParser() : this(DefaultMaxInputSize)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="IniParser"/> with a custom size limit.
    /// </summary>
    /// <param name="maxInputSize">
    /// Maximum file size in bytes (for <see cref="ParseFile"/>) or content length in
    /// characters (for <see cref="ParseString"/>) before an <see cref="EdsParseException"/>
    /// is thrown.
    /// </param>
    public IniParser(long maxInputSize)
    {
        _maxInputSize = maxInputSize;
    }

    /// <summary>
    /// Parses an EDS/DCF file and returns sections with their key-value pairs.
    /// </summary>
    /// <param name="filePath">Path to the EDS/DCF file</param>
    /// <returns>Dictionary where key is section name (lowercase) and value is key-value pairs</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="EdsParseException">Thrown when the file exceeds the configured size limit.</exception>
    public Dictionary<string, Dictionary<string, string>> ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"EDS/DCF file not found: {filePath}");

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > _maxInputSize)
            throw new EdsParseException(
                string.Format(CultureInfo.InvariantCulture,
                    "File '{0}' is too large ({1:N0} bytes). Maximum supported size is {2:N0} bytes.",
                    filePath, fileInfo.Length, _maxInputSize));

        return ParseLines(File.ReadLines(filePath));
    }

    /// <summary>
    /// Parses EDS/DCF content from a string.
    /// </summary>
    /// <param name="content">EDS/DCF file content as string</param>
    /// <returns>Dictionary where key is section name (lowercase) and value is key-value pairs</returns>
    /// <exception cref="EdsParseException">Thrown when the content length exceeds the configured size limit.</exception>
    public Dictionary<string, Dictionary<string, string>> ParseString(string content)
    {
        if (content.Length > _maxInputSize)
            throw new EdsParseException(
                string.Format(CultureInfo.InvariantCulture,
                    "Content is too large ({0:N0} characters). Maximum supported size is {1:N0} characters.",
                    content.Length, _maxInputSize));

        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return ParseLines(lines);
    }

    /// <summary>
    /// Parses lines of an EDS/DCF file.
    /// </summary>
    private Dictionary<string, Dictionary<string, string>> ParseLines(IEnumerable<string> lines)
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
