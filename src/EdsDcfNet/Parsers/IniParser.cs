namespace EdsDcfNet.Parsers;

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using EdsDcfNet.Exceptions;

/// <summary>
/// Low-level INI file parser for EDS/DCF files.
/// Parses INI-style files with sections and key-value pairs.
/// All members are static; no instantiation is required.
/// </summary>
public static class IniParser
{
    private static readonly char[] LineEndChars = { '\r', '\n' };
    /// <summary>
    /// Default maximum input size (10 MB) used by <see cref="ParseFile"/> and
    /// <see cref="ParseString"/> to guard against unbounded memory consumption.
    /// </summary>
    public const long DefaultMaxInputSize = 10L * 1024 * 1024;

    /// <summary>
    /// Parses an EDS/DCF file and returns sections with their key-value pairs.
    /// </summary>
    /// <param name="filePath">Path to the EDS/DCF file</param>
    /// <param name="maxInputSize">
    /// Maximum file size in bytes before an <see cref="EdsParseException"/> is thrown.
    /// Defaults to <see cref="DefaultMaxInputSize"/> (10 MB).
    /// </param>
    /// <returns>Dictionary where key is section name and value is key-value pairs</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="EdsParseException">Thrown when the file exceeds the configured size limit.</exception>
    public static Dictionary<string, Dictionary<string, string>> ParseFile(
        string filePath,
        long maxInputSize = DefaultMaxInputSize)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"EDS/DCF file not found: {filePath}", filePath);

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > maxInputSize)
            throw new EdsParseException(
                string.Format(CultureInfo.InvariantCulture,
                    "File '{0}' is too large ({1:N0} bytes). Maximum supported size is {2:N0} bytes.",
                    filePath, fileInfo.Length, maxInputSize));

        return ParseLines(File.ReadLines(filePath));
    }

    /// <summary>
    /// Parses an EDS/DCF file asynchronously and returns sections with their key-value pairs.
    /// </summary>
    /// <param name="filePath">Path to the EDS/DCF file</param>
    /// <param name="maxInputSize">
    /// Maximum file size in bytes before an <see cref="EdsParseException"/> is thrown.
    /// Defaults to <see cref="DefaultMaxInputSize"/> (10 MB).
    /// </param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Dictionary where key is section name and value is key-value pairs</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="EdsParseException">Thrown when the file exceeds the configured size limit.</exception>
    public static async Task<Dictionary<string, Dictionary<string, string>>> ParseFileAsync(
        string filePath,
        long maxInputSize = DefaultMaxInputSize,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"EDS/DCF file not found: {filePath}", filePath);

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > maxInputSize)
            throw new EdsParseException(
                string.Format(CultureInfo.InvariantCulture,
                    "File '{0}' is too large ({1:N0} bytes). Maximum supported size is {2:N0} bytes.",
                    filePath, fileInfo.Length, maxInputSize));

        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream);

        return await ParseReaderAsync(reader, maxInputSize, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses EDS/DCF content from a readable stream.
    /// </summary>
    /// <param name="stream">Input stream containing INI content. The stream remains open after parsing.</param>
    /// <param name="maxInputSize">
    /// Maximum decoded content length in characters before an <see cref="EdsParseException"/> is thrown.
    /// This limit applies to parsed text content, not raw byte length.
    /// Defaults to <see cref="DefaultMaxInputSize"/> (10 MB).
    /// </param>
    /// <returns>Dictionary where key is section name and value is key-value pairs</returns>
    public static Dictionary<string, Dictionary<string, string>> ParseStream(
        Stream stream,
        long maxInputSize = DefaultMaxInputSize)
    {
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
        return ParseReader(reader, maxInputSize);
    }

    /// <summary>
    /// Parses EDS/DCF content from a readable stream asynchronously.
    /// </summary>
    /// <param name="stream">Input stream containing INI content. The stream remains open after parsing.</param>
    /// <param name="maxInputSize">
    /// Maximum decoded content length in characters before an <see cref="EdsParseException"/> is thrown.
    /// This limit applies to parsed text content, not raw byte length.
    /// Defaults to <see cref="DefaultMaxInputSize"/> (10 MB).
    /// </param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Dictionary where key is section name and value is key-value pairs</returns>
    public static async Task<Dictionary<string, Dictionary<string, string>>> ParseStreamAsync(
        Stream stream,
        long maxInputSize = DefaultMaxInputSize,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
        return await ParseReaderAsync(reader, maxInputSize, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses EDS/DCF content from a string.
    /// </summary>
    /// <param name="content">EDS/DCF file content as string</param>
    /// <param name="maxInputSize">
    /// Maximum content length in characters before an <see cref="EdsParseException"/> is thrown.
    /// Defaults to <see cref="DefaultMaxInputSize"/> (10 MB).
    /// </param>
    /// <returns>Dictionary where key is section name and value is key-value pairs</returns>
    /// <exception cref="EdsParseException">Thrown when the content length exceeds the configured size limit.</exception>
    public static Dictionary<string, Dictionary<string, string>> ParseString(
        string content,
        long maxInputSize = DefaultMaxInputSize)
    {
        if (content.Length > maxInputSize)
            throw new EdsParseException(
                string.Format(CultureInfo.InvariantCulture,
                    "Content is too large ({0:N0} characters). Maximum supported size is {1:N0} characters.",
                    content.Length, maxInputSize));

        // Split on CR/LF as independent line terminators. RemoveEmptyEntries drops empty
        // segments produced by splitting on both '\r' and '\n' (e.g., within CRLF); blank/
        // whitespace-only lines are already ignored by ParseLine. This also means line
        // numbers in exceptions from ParseString can differ from those produced by ParseReader.
        var lines = content.Split(LineEndChars, StringSplitOptions.RemoveEmptyEntries);
        return ParseLines(lines);
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

    private static Dictionary<string, Dictionary<string, string>> ParseLines(IEnumerable<string> lines)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;
        var lineNumber = 0;

        foreach (var rawLine in lines)
        {
            lineNumber++;
            ParseLine(rawLine, lineNumber, ref currentSection, sections);
        }

        return sections;
    }

    private static Dictionary<string, Dictionary<string, string>> ParseReader(
        StreamReader reader,
        long maxInputSize)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;
        var lineNumber = 0;
        long totalChars = 0;
        var currentLine = new StringBuilder();
        var skipNextLineFeed = false;
        var buffer = new char[4096];

        while (true)
        {
            var charsRead = reader.Read(buffer, 0, buffer.Length);
            if (charsRead == 0)
                break;

            for (var i = 0; i < charsRead; i++)
            {
                EnsureContentWithinSizeLimit(ref totalChars, maxInputSize);
                ProcessReaderCharacter(buffer[i], ref skipNextLineFeed, currentLine, ref lineNumber, ref currentSection, sections);
            }
        }

        FlushPendingLine(currentLine, ref lineNumber, ref currentSection, sections);
        return sections;
    }

    private static async Task<Dictionary<string, Dictionary<string, string>>> ParseReaderAsync(
        StreamReader reader,
        long maxInputSize,
        CancellationToken cancellationToken)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;
        var lineNumber = 0;
        long totalChars = 0;
        var currentLine = new StringBuilder();
        var skipNextLineFeed = false;
        var buffer = new char[4096];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
#if NET10_0_OR_GREATER
            var charsRead = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
#else
            var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
#endif
            if (charsRead == 0)
                break;

            for (var i = 0; i < charsRead; i++)
            {
                EnsureContentWithinSizeLimit(ref totalChars, maxInputSize);
                ProcessReaderCharacter(buffer[i], ref skipNextLineFeed, currentLine, ref lineNumber, ref currentSection, sections);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        FlushPendingLine(currentLine, ref lineNumber, ref currentSection, sections);
        return sections;
    }

    private static void EnsureContentWithinSizeLimit(ref long totalChars, long maxInputSize)
    {
        totalChars++;
        if (totalChars > maxInputSize)
        {
            throw new EdsParseException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Content is too large ({0:N0} characters). Maximum supported size is {1:N0} characters.",
                    totalChars,
                    maxInputSize));
        }
    }

    private static void ProcessReaderCharacter(
        char character,
        ref bool skipNextLineFeed,
        StringBuilder currentLine,
        ref int lineNumber,
        ref string? currentSection,
        Dictionary<string, Dictionary<string, string>> sections)
    {
        if (skipNextLineFeed)
        {
            skipNextLineFeed = false;
            if (character == '\n')
            {
                return;
            }
        }

        if (character == '\r')
        {
            lineNumber++;
            ParseLine(currentLine.ToString(), lineNumber, ref currentSection, sections);
            currentLine.Clear();
            skipNextLineFeed = true;
            return;
        }

        if (character == '\n')
        {
            lineNumber++;
            ParseLine(currentLine.ToString(), lineNumber, ref currentSection, sections);
            currentLine.Clear();
            return;
        }

        currentLine.Append(character);
    }

    private static void FlushPendingLine(
        StringBuilder currentLine,
        ref int lineNumber,
        ref string? currentSection,
        Dictionary<string, Dictionary<string, string>> sections)
    {
        if (currentLine.Length == 0)
            return;

        lineNumber++;
        ParseLine(currentLine.ToString(), lineNumber, ref currentSection, sections);
    }

    private static void ParseLine(
        string rawLine,
        int lineNumber,
        ref string? currentSection,
        Dictionary<string, Dictionary<string, string>> sections)
    {
        var line = rawLine.Trim();

        // Skip empty lines and comments
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';'))
            return;

        // Check for section header
        if (line.StartsWith('[') && line.EndsWith(']'))
        {
            currentSection = line[1..^1].Trim();

            if (!sections.ContainsKey(currentSection))
            {
                sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            return;
        }

        // Parse key-value pair
        var equalIndex = line.IndexOf('=');
        if (equalIndex <= 0)
            return;

        if (currentSection == null)
        {
            throw new EdsParseException($"Key-value pair found outside of any section at line {lineNumber}", lineNumber);
        }

        var key = line[..equalIndex].Trim();
        var value = equalIndex < line.Length - 1
            ? line[(equalIndex + 1)..].Trim()
            : string.Empty;

        sections[currentSection][key] = value;
    }

    private static void ThrowIfNull(object? value, string parameterName)
    {
        if (value == null)
            throw new ArgumentNullException(parameterName);
    }
}
