namespace EdsDcfNet.Diagnostics;

/// <summary>
/// A single lenient-mode repair reported while reading a CANopen file: the parser accepted
/// malformed input and coerced it to a default instead of throwing.
/// </summary>
/// <remarks>
/// Diagnostics are collected only when reading through the
/// <c>Read*WithDiagnostics</c> methods of the format entry points
/// (<see cref="CanOpenFile.Eds"/> and siblings). Direct <c>*Reader</c> APIs stay lenient
/// and silent. In strict mode (<see cref="CanOpenFileOptions.StrictParsing"/>) the same
/// conditions throw <see cref="Exceptions.EdsParseException"/> carrying the same
/// <see cref="Code"/> instead.
/// </remarks>
public sealed class ParseDiagnostic
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParseDiagnostic"/> class.
    /// </summary>
    /// <param name="severity">Severity of the reported repair.</param>
    /// <param name="code">Stable machine-readable identifier (see <see cref="ParseDiagnosticCodes"/>).</param>
    /// <param name="path">Section/key (INI formats) or XPath-like location (XML formats); empty when no location context exists.</param>
    /// <param name="message">Human-readable description of the deviation.</param>
    /// <param name="line">Source line number when known (INI formats).</param>
    /// <param name="rawValue">The offending raw input value when applicable.</param>
    /// <param name="coercedTo">The value lenient mode substituted, when a value was substituted.</param>
    public ParseDiagnostic(
        ParseSeverity severity,
        string code,
        string path,
        string message,
        int? line = null,
        string? rawValue = null,
        string? coercedTo = null)
    {
        Severity = severity;
        Code = code;
        Path = path;
        Message = message;
        Line = line;
        RawValue = rawValue;
        CoercedTo = coercedTo;
    }

    /// <summary>Gets the severity of the reported repair.</summary>
    public ParseSeverity Severity { get; }

    /// <summary>Gets the stable machine-readable identifier (see <see cref="ParseDiagnosticCodes"/>).</summary>
    public string Code { get; }

    /// <summary>
    /// Gets the section/key (INI formats) or XPath-like location (XML formats) of the
    /// deviation, following the same convention as
    /// <see cref="Validation.ValidationIssue.Path"/>. Empty when the reporting site has no
    /// location context (for example shared token converters).
    /// </summary>
    public string Path { get; }

    /// <summary>Gets the human-readable description of the deviation.</summary>
    public string Message { get; }

    /// <summary>Gets the source line number when known (INI formats).</summary>
    public int? Line { get; }

    /// <summary>Gets the offending raw input value when applicable.</summary>
    public string? RawValue { get; }

    /// <summary>Gets the value lenient mode substituted, when a value was substituted.</summary>
    public string? CoercedTo { get; }

    /// <inheritdoc />
    public override string ToString() =>
        Line.HasValue
            ? $"[{Severity}] {Code} at {Path}:{Line.Value}: {Message}"
            : $"[{Severity}] {Code} at {Path}: {Message}";
}
