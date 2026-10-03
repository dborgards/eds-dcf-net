namespace EdsDcfNet.Parsers;

using System.Collections.Generic;
using System.Globalization;
using EdsDcfNet.Exceptions;

/// <summary>
/// Low-level INI file parser for EDS/DCF files.
/// Parses INI-style files with sections and key-value pairs.
/// All members are static; no instantiation is required.
/// </summary>
/// <remarks>
/// <para>
/// Duplicate keys within a section use last-write-wins by default (lenient).
/// Pass <c>strictParsing: true</c> or set <see cref="CanOpenFileOptions.StrictParsing"/>
/// on facade reads to throw <see cref="EdsParseException"/> on duplicates.
/// </para>
/// <para>
/// File and stream bytes are decoded automatically unless
/// <see cref="CanOpenFileOptions.Encoding"/> is set on a facade read: a byte-order mark
/// wins, otherwise strict UTF-8 is tried and, on <see cref="System.Text.DecoderFallbackException"/>,
/// those same bytes are decoded as ISO-8859-1. A leading UTF-8 byte-order mark is excluded
/// from both decodes. Stream reads still limit the
/// decoded character count; the raw buffer is capped by <see cref="InputBufferLimit"/>.
/// </para>
/// </remarks>
public static class IniParser
{

    /// <summary>
    /// Sentinel <c>currentSection</c> after a malformed header in lenient mode: the keys that follow
    /// are dropped (not attributed to the previous section) until the next valid header. Compared by
    /// reference; it is never added to the parsed sections.
    /// </summary>
    private static readonly string DiscardedSection = new string('\0', 1);
    /// <summary>
    /// Default maximum input size (10 MB) used by parsing methods such as
    /// <see cref="ParseFile(string, long)"/>, <see cref="ParseFileAsync"/>,
    /// <see cref="ParseStream(Stream, long)"/>, <see cref="ParseStreamAsync"/>,
    /// and <see cref="ParseString(string, long)"/> to guard against
    /// unbounded memory consumption.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="ReaderDefaults.DefaultMaxInputSize"/>. Kept for
    /// backward compatibility; prefer <see cref="ReaderDefaults.DefaultMaxInputSize"/>
    /// in new code.
    /// </remarks>
    public const long DefaultMaxInputSize = ReaderDefaults.DefaultMaxInputSize;

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
        => ParseFile(filePath, maxInputSize, strictParsing: false);

    /// <summary>
    /// Parses an EDS/DCF file and returns sections with their key-value pairs.
    /// </summary>
    /// <param name="filePath">Path to the EDS/DCF file</param>
    /// <param name="maxInputSize">
    /// Maximum file size in bytes before an <see cref="EdsParseException"/> is thrown.
    /// </param>
    /// <param name="strictParsing">
    /// When <see langword="true"/>, duplicate keys in a section, malformed section headers,
    /// lines without <c>=</c> (or with an empty key) and duplicate section headers throw
    /// <see cref="EdsParseException"/> instead of being repaired (last-write-wins, ignored, merged).
    /// A line starting with <c>#</c> without <c>=</c> is ignored in both modes.
    /// </param>
    /// <returns>Dictionary where key is section name and value is key-value pairs</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="EdsParseException">Thrown when the file exceeds the configured size limit.</exception>
    public static Dictionary<string, Dictionary<string, string>> ParseFile(
        string filePath,
        long maxInputSize,
        bool strictParsing)
    {
        using (StrictParsingScope.Enter(strictParsing || StrictParsingScope.IsEnabled))
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"EDS/DCF file not found: {filePath}", filePath);

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > maxInputSize)
                throw new EdsParseException(
                    string.Format(CultureInfo.InvariantCulture,
                        "File '{0}' is too large ({1:N0} bytes). Maximum supported size is {2:N0} bytes.",
                        filePath, fileInfo.Length, maxInputSize));

            // Buffer through the byte-limited stream so MaxInputSize stays a byte cap
            // (guards TOCTOU if the file grows after the FileInfo.Length check) and the
            // same bytes can be decoded twice when UTF-8 fails.
            using var stream = OpenFileWithByteLimit(filePath, maxInputSize, useAsync: false);
            var bytes = ReadToEnd(stream);
            return ParseDecodedBytes(bytes, maxInputSize);
        }
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
        // StrictParsing is supplied via StrictParsingScope (facade / sync overloads).
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"EDS/DCF file not found: {filePath}", filePath);

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > maxInputSize)
            throw new EdsParseException(
                string.Format(CultureInfo.InvariantCulture,
                    "File '{0}' is too large ({1:N0} bytes). Maximum supported size is {2:N0} bytes.",
                    filePath, fileInfo.Length, maxInputSize));

        using var stream = OpenFileWithByteLimit(filePath, maxInputSize, useAsync: true);
        var bytes = await ReadToEndAsync(stream, cancellationToken).ConfigureAwait(false);
        return ParseDecodedBytes(bytes, maxInputSize);
    }

    private static ByteLimitingStream OpenFileWithByteLimit(
        string filePath,
        long maxInputSize,
        bool useAsync)
    {
        var message = string.Format(
            CultureInfo.InvariantCulture,
            "File '{0}' is too large. Maximum supported size is {1:N0} bytes.",
            filePath,
            maxInputSize);

        return ByteLimitingStream.OpenFile(filePath, maxInputSize, message, useAsync);
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
        => ParseStream(stream, maxInputSize, strictParsing: false);

    /// <summary>
    /// Parses EDS/DCF content from a readable stream.
    /// </summary>
    /// <param name="stream">Input stream containing INI content. The stream remains open after parsing.</param>
    /// <param name="maxInputSize">
    /// Maximum decoded content length in characters before an <see cref="EdsParseException"/> is thrown.
    /// This limit applies to parsed text content, not raw byte length.
    /// </param>
    /// <param name="strictParsing">
    /// When <see langword="true"/>, duplicate keys in a section, malformed section headers,
    /// lines without <c>=</c> (or with an empty key) and duplicate section headers throw
    /// <see cref="EdsParseException"/> instead of being repaired (last-write-wins, ignored, merged).
    /// A line starting with <c>#</c> without <c>=</c> is ignored in both modes.
    /// </param>
    /// <returns>Dictionary where key is section name and value is key-value pairs</returns>
    public static Dictionary<string, Dictionary<string, string>> ParseStream(
        Stream stream,
        long maxInputSize,
        bool strictParsing)
    {
        using (StrictParsingScope.Enter(strictParsing || StrictParsingScope.IsEnabled))
        {
            ThrowIfNull(stream, nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));

            var bytes = ReadBounded(stream, StreamByteCap(maxInputSize), StreamByteCapMessage(maxInputSize));
            return ParseDecodedBytes(bytes, maxInputSize);
        }
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
        // StrictParsing is supplied via StrictParsingScope (facade / sync overloads).
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));

        var bytes = await ReadBoundedAsync(stream, StreamByteCap(maxInputSize), StreamByteCapMessage(maxInputSize), cancellationToken).ConfigureAwait(false);
        return ParseDecodedBytes(bytes, maxInputSize);
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
        => ParseString(content, maxInputSize, strictParsing: false);

    /// <summary>
    /// Parses EDS/DCF content from a string.
    /// </summary>
    /// <param name="content">EDS/DCF file content as string</param>
    /// <param name="maxInputSize">
    /// Maximum content length in characters before an <see cref="EdsParseException"/> is thrown.
    /// </param>
    /// <param name="strictParsing">
    /// When <see langword="true"/>, duplicate keys in a section, malformed section headers,
    /// lines without <c>=</c> (or with an empty key) and duplicate section headers throw
    /// <see cref="EdsParseException"/> instead of being repaired (last-write-wins, ignored, merged).
    /// A line starting with <c>#</c> without <c>=</c> is ignored in both modes.
    /// </param>
    /// <returns>Dictionary where key is section name and value is key-value pairs</returns>
    /// <exception cref="EdsParseException">Thrown when the content length exceeds the configured size limit.</exception>
    public static Dictionary<string, Dictionary<string, string>> ParseString(
        string content,
        long maxInputSize,
        bool strictParsing)
    {
        using (StrictParsingScope.Enter(strictParsing || StrictParsingScope.IsEnabled))
        {
            if (content.Length > maxInputSize)
                throw new EdsParseException(
                    string.Format(CultureInfo.InvariantCulture,
                        "Content is too large ({0:N0} characters). Maximum supported size is {1:N0} characters.",
                        content.Length, maxInputSize));

            return ParseLines(SplitLines(content));
        }
    }

    /// <summary>
    /// Splits content into physical lines exactly like the stream path: CR, LF and CRLF each end
    /// one line, blank lines are kept (so line numbers match the file), and a trailing segment
    /// without terminator counts only when it is non-empty.
    /// </summary>
    private static IEnumerable<string> SplitLines(string content)
    {
        var start = 0;
        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (c != '\r' && c != '\n')
                continue;

            yield return content.Substring(start, i - start);
            if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                i++;
            start = i + 1;
        }

        if (start < content.Length)
            yield return content.Substring(start);
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
        if (line.StartsWith('['))
        {
            ParseSectionHeader(line, lineNumber, ref currentSection, sections);
            return;
        }

        // Parse key-value pair
        var equalIndex = line.IndexOf('=');
        if (equalIndex <= 0)
        {
            // Real-world files comment lines out with '#'; such a line without '=' was always
            // ignored silently. ('#' is not a general comment character: "#Key=Value" stays a key.)
            if (equalIndex < 0 && line.StartsWith('#'))
                return;

            ReportMissingEquals(line, lineNumber, currentSection);
            return;
        }

        if (currentSection == null)
        {
            throw new EdsParseException($"Key-value pair found outside of any section at line {lineNumber}", lineNumber);
        }

        // Keys below a malformed header (lenient mode) are dropped; the header was already reported.
        if (ReferenceEquals(currentSection, DiscardedSection))
            return;

        var key = line[..equalIndex].Trim();
        var value = equalIndex < line.Length - 1
            ? line[(equalIndex + 1)..].Trim()
            : string.Empty;

        var section = (IniSectionDictionary)sections[currentSection];
        if (section.TryGetValue(key, out var previousValue))
        {
            Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
                Diagnostics.ParseSeverity.Warning,
                Diagnostics.ParseDiagnosticCodes.IniDuplicateKey,
                path: currentSection + "." + key,
                line: lineNumber,
                rawValue: value,
                coercedTo: value,
                message: string.Format(
                    CultureInfo.InvariantCulture,
                    "Duplicate key '{0}' in section '{1}' at line {2}; the earlier value '{3}' is overwritten (last write wins).",
                    key,
                    currentSection,
                    lineNumber,
                    previousValue)));

            if (StrictParsingScope.IsEnabled)
            {
                throw new EdsParseException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Duplicate key '{0}' in section '{1}' at line {2}.",
                        key,
                        currentSection,
                        lineNumber),
                    currentSection,
                    lineNumber)
                {
                    Code = Diagnostics.ParseDiagnosticCodes.IniDuplicateKey
                };
            }
        }

        section.Set(key, value);
        IniKeyLines.Record(sections, currentSection, key, lineNumber);
    }

    private static void ParseSectionHeader(
        string line,
        int lineNumber,
        ref string? currentSection,
        Dictionary<string, Dictionary<string, string>> sections)
    {
        // A header is "[name]" optionally followed by a ';' comment. The name ends at the first ']'.
        var closeIndex = line.IndexOf(']');
        var trailing = closeIndex < 0 ? string.Empty : line[(closeIndex + 1)..].Trim();
        if (closeIndex < 0 || (trailing.Length > 0 && !trailing.StartsWith(';')))
        {
            ReportMalformedHeader(line, lineNumber);
            currentSection = DiscardedSection;
            return;
        }

        var name = line[1..closeIndex].Trim();
        if (sections.ContainsKey(name))
        {
            ReportDuplicateSection(name, lineNumber);
        }
        else
        {
            sections[name] = new IniSectionDictionary();
        }

        currentSection = name;
    }

    private static void ReportMalformedHeader(string line, int lineNumber)
    {
        var message = string.Format(
            CultureInfo.InvariantCulture,
            "Malformed section header '{0}' at line {1}; the header and the keys that follow it are ignored.",
            line,
            lineNumber);

        Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
            Diagnostics.ParseSeverity.Warning,
            Diagnostics.ParseDiagnosticCodes.IniMalformedSectionHeader,
            path: "line " + lineNumber.ToString(CultureInfo.InvariantCulture),
            message: message,
            line: lineNumber,
            rawValue: line));

        if (StrictParsingScope.IsEnabled)
        {
            throw new EdsParseException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Malformed section header '{0}' at line {1}.",
                    line,
                    lineNumber),
                lineNumber)
            {
                Code = Diagnostics.ParseDiagnosticCodes.IniMalformedSectionHeader
            };
        }
    }

    private static void ReportMissingEquals(string line, int lineNumber, string? currentSection)
    {
        // Inside a discarded (malformed) section the header was already reported.
        if (ReferenceEquals(currentSection, DiscardedSection))
            return;

        var section = currentSection;

        Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
            Diagnostics.ParseSeverity.Warning,
            Diagnostics.ParseDiagnosticCodes.IniMissingEquals,
            path: section ?? "line " + lineNumber.ToString(CultureInfo.InvariantCulture),
            message: string.Format(
                CultureInfo.InvariantCulture,
                "Line {0} '{1}' is not a 'key=value' pair (missing '=' or empty key); the line is ignored.",
                lineNumber,
                line),
            line: lineNumber,
            rawValue: line));

        if (StrictParsingScope.IsEnabled)
        {
            var message = string.Format(
                CultureInfo.InvariantCulture,
                "Line {0} '{1}' is not a 'key=value' pair (missing '=' or empty key).",
                lineNumber,
                line);
            var exception = section == null
                ? new EdsParseException(message, lineNumber)
                : new EdsParseException(message, section, lineNumber);
            exception.Code = Diagnostics.ParseDiagnosticCodes.IniMissingEquals;
            throw exception;
        }
    }

    private static void ReportDuplicateSection(string name, int lineNumber)
    {
        Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
            Diagnostics.ParseSeverity.Warning,
            Diagnostics.ParseDiagnosticCodes.IniDuplicateSection,
            path: name,
            message: string.Format(
                CultureInfo.InvariantCulture,
                "Duplicate section '{0}' at line {1}; its keys are merged into the earlier section.",
                name,
                lineNumber),
            line: lineNumber));

        if (StrictParsingScope.IsEnabled)
        {
            throw new EdsParseException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Duplicate section '{0}' at line {1}.",
                    name,
                    lineNumber),
                name,
                lineNumber)
            {
                Code = Diagnostics.ParseDiagnosticCodes.IniDuplicateSection
            };
        }
    }

    private const int ReadChunkSize = 8192;

    private static Dictionary<string, Dictionary<string, string>> ParseDecodedBytes(byte[] bytes, long maxInputSize)
    {
        var content = IniTextDecoder.Decode(bytes);
        if ((long)content.Length > maxInputSize)
        {
            throw new EdsParseException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Content is too large ({0:N0} characters). Maximum supported size is {1:N0} characters.",
                    content.Length,
                    maxInputSize));
        }

        return ParseLines(SplitLines(content));
    }

    private static long StreamByteCap(long maxInputSize)
        => InputBufferLimit.GetMaxBufferedByteCount(maxInputSize, FileEncodingScope.CurrentRead);

    private static string StreamByteCapMessage(long maxInputSize)
        => string.Format(
            CultureInfo.InvariantCulture,
            "Content is too large. Maximum supported size is {0:N0} characters.",
            maxInputSize);

    private static byte[] ReadToEnd(Stream stream)
        => ReadBounded(stream, long.MaxValue, exceededMessage: string.Empty);

    private static Task<byte[]> ReadToEndAsync(Stream stream, CancellationToken cancellationToken)
        => ReadBoundedAsync(stream, long.MaxValue, exceededMessage: string.Empty, cancellationToken);

    /// <summary>
    /// Reads at most <paramref name="maxBytes"/>, plus one probe byte when the stream
    /// continues, then throws. The remainder of an overlong stream is not consumed.
    /// </summary>
    private static byte[] ReadBounded(Stream stream, long maxBytes, string exceededMessage)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[ReadChunkSize];
        long total = 0;
        while (true)
        {
            var want = NextReadSize(maxBytes, total);
            var read = stream.Read(chunk, 0, want);
            if (read == 0)
                break;

            total += read;
            if (total > maxBytes)
                throw new EdsParseException(exceededMessage);

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream stream,
        long maxBytes,
        string exceededMessage,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[ReadChunkSize];
        long total = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var want = NextReadSize(maxBytes, total);
#if NET10_0_OR_GREATER
            var read = await stream.ReadAsync(chunk.AsMemory(0, want), cancellationToken).ConfigureAwait(false);
#else
            var read = await stream.ReadAsync(chunk, 0, want, cancellationToken).ConfigureAwait(false);
#endif
            if (read == 0)
                break;

            total += read;
            if (total > maxBytes)
                throw new EdsParseException(exceededMessage);

            buffer.Write(chunk, 0, read);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return buffer.ToArray();
    }

    internal static int NextReadSize(long maxBytes, long total)
    {
        if (maxBytes == long.MaxValue)
            return ReadChunkSize;

        var remaining = maxBytes - total;
        if (remaining < 0)
            return 0;

        var probe = remaining >= int.MaxValue ? int.MaxValue : (int)remaining + 1;
        return probe < ReadChunkSize ? probe : ReadChunkSize;
    }

    private static void ThrowIfNull(object? value, string parameterName)
    {
        if (value == null)
            throw new ArgumentNullException(parameterName);
    }
}
