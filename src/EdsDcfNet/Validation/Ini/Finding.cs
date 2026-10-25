namespace EdsDcfNet.Validation;

using System.Globalization;

/// <summary>Severity of a line-aware INI validation issue.</summary>
public enum IniValidationSeverity
{
    /// <summary>A non-fatal conformance or interoperability problem.</summary>
    Warning,

    /// <summary>An invalid value or structure.</summary>
    Error,
}

/// <summary>A single problem found in EDS or DCF source text.</summary>
/// <param name="Severity">How serious the issue is.</param>
/// <param name="Code">Stable rule code (for example <c>VAL001</c>).</param>
/// <param name="File">Source name or path supplied by the caller.</param>
/// <param name="Line">1-based line number, when known.</param>
/// <param name="Section">INI section the issue belongs to, when known.</param>
/// <param name="Key">INI key the issue belongs to, when known.</param>
/// <param name="Value">Raw value that triggered the issue, when relevant.</param>
/// <param name="Message">Human-readable description.</param>
public sealed record IniValidationIssue(
    IniValidationSeverity Severity,
    string Code,
    string File,
    int? Line,
    string? Section,
    string? Key,
    string? Value,
    string Message)
{
    /// <summary>Formats the issue as a compiler-style single line.</summary>
    public string ToDisplayString()
    {
        var location = Line.HasValue
            ? string.Format(CultureInfo.InvariantCulture, "{0}:{1}", File, Line.Value)
            : File;

        var subject = Section is null ? string.Empty : "[" + Section + "]";
        if (Key is not null)
        {
            subject += (subject.Length > 0 ? " " : string.Empty) + Key;
            if (Value is not null)
            {
                subject += "=" + Value;
            }
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}: {1} {2}{3}: {4}",
            location,
            Severity.ToString().ToLowerInvariant(),
            Code,
            subject.Length > 0 ? " " + subject : string.Empty,
            Message);
    }
}
