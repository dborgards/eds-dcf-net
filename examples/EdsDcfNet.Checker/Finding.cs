namespace EdsDcfNet.Checker;

using System.Globalization;

/// <summary>Severity of a checker finding.</summary>
public enum Severity
{
    Info,
    Warning,
    Error,
}

/// <summary>A single problem found in an EDS/DCF file.</summary>
/// <param name="Severity">How serious the finding is.</param>
/// <param name="Code">Stable rule code (for example <c>VAL001</c>).</param>
/// <param name="File">Path of the checked file.</param>
/// <param name="Line">1-based line number, when known.</param>
/// <param name="Section">INI section the finding belongs to, when known.</param>
/// <param name="Key">INI key the finding belongs to, when known.</param>
/// <param name="Value">Raw value that triggered the finding, when relevant.</param>
/// <param name="Message">Human-readable description.</param>
public sealed record Finding(
    Severity Severity,
    string Code,
    string File,
    int? Line,
    string? Section,
    string? Key,
    string? Value,
    string Message)
{
    /// <summary>Formats the finding as a compiler-style single line.</summary>
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
