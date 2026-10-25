namespace EdsDcfNet.Validation;

/// <summary>
/// Validates EDS and DCF source text directly, retaining source lines and reporting all
/// detectable problems instead of stopping at the first reader error.
/// </summary>
/// <remarks>
/// These rules complement <see cref="CanOpenModelValidator"/>: they also cover malformed
/// source structure, duplicate sections and keys, non-canonical section names and raw values
/// that cannot be represented by a loaded model. The validator does not modify the input.
/// </remarks>
public static class CanOpenIniValidator
{
    /// <summary>Validates EDS or DCF source text.</summary>
    /// <param name="text">Complete INI source text.</param>
    /// <param name="format">INI dialect to validate.</param>
    /// <param name="sourceName">Name included in each issue; defaults to <c>&lt;memory&gt;</c>.</param>
    /// <param name="cancellationToken">Token observed while scanning and validating the input.</param>
    /// <returns>All raw-source findings, ordered by source line, severity and rule code.</returns>
    public static IReadOnlyList<IniValidationIssue> ValidateString(
        string text,
        CanOpenIniFormat format,
        string? sourceName = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNull(text);

        using var reader = new StringReader(text);
        return Validate(reader, sourceName ?? "<memory>", format, cancellationToken);
    }

    /// <summary>Validates an EDS or DCF file without stopping at the first invalid entry.</summary>
    /// <param name="filePath">Path of the file to read.</param>
    /// <param name="format">INI dialect to validate; it is not inferred from the extension.</param>
    /// <param name="cancellationToken">Token observed while scanning and validating the input.</param>
    /// <returns>All raw-source findings, ordered by source line, severity and rule code.</returns>
    public static IReadOnlyList<IniValidationIssue> ValidateFile(
        string filePath,
        CanOpenIniFormat format,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNull(filePath);

        using var reader = File.OpenText(filePath);
        return Validate(reader, filePath, format, cancellationToken);
    }

    private static List<IniValidationIssue> Validate(
        TextReader reader,
        string sourceName,
        CanOpenIniFormat format,
        CancellationToken cancellationToken)
    {
        if (format is not (CanOpenIniFormat.Eds or CanOpenIniFormat.Dcf))
            throw new ArgumentOutOfRangeException(nameof(format));

        var findings = new List<IniValidationIssue>();
        var document = RawIniDocument.Parse(reader, sourceName, findings, cancellationToken);
        var isDcf = format == CanOpenIniFormat.Dcf;
        new MandatoryFieldsChecker(sourceName, document, isDcf, findings).Run();
        new RawObjectChecker(sourceName, document, isDcf, findings, cancellationToken).Run();
        cancellationToken.ThrowIfCancellationRequested();

        return findings
            .Distinct()
            .OrderBy(f => f.Line ?? 0)
            .ThenByDescending(f => f.Severity)
            .ThenBy(f => f.Code, StringComparer.Ordinal)
            .ToList();
    }

    private static void ThrowIfNull(
        object? value,
        [System.Runtime.CompilerServices.CallerArgumentExpression("value")] string parameterName = "")
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value, parameterName);
#else
        if (value is null)
            throw new ArgumentNullException(parameterName);
#endif
    }
}
