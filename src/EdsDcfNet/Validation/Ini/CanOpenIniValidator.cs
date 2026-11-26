namespace EdsDcfNet.Validation;

using EdsDcfNet.Diagnostics;

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
        return ValidateString(text, format, sourceName, CanOpenIniValidationOptions.Default, cancellationToken);
    }

    /// <summary>Validates EDS or DCF source text with severity overrides and optional reader and model passes.</summary>
    /// <param name="text">Complete INI source text.</param>
    /// <param name="format">INI dialect to validate.</param>
    /// <param name="sourceName">Name included in each issue; <see langword="null"/> uses <c>&lt;memory&gt;</c>.</param>
    /// <param name="options">Severity overrides and other validation options.</param>
    /// <param name="cancellationToken">Token observed while scanning and validating the input.</param>
    /// <returns>All raw-source findings after overrides, ordered by source line, severity and rule code.</returns>
    public static IReadOnlyList<IniValidationIssue> ValidateString(
        string text,
        CanOpenIniFormat format,
        string? sourceName,
        CanOpenIniValidationOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNull(text);
        ThrowIfNull(options);

        using var reader = new StringReader(text);
        return Validate(
            reader,
            sourceName ?? "<memory>",
            format,
            options,
            () => format == CanOpenIniFormat.Dcf
                ? LibraryPass.Dcf(CanOpenFile.Dcf.ReadStringWithDiagnostics(text))
                : LibraryPass.Eds(CanOpenFile.Eds.ReadStringWithDiagnostics(text)),
            cancellationToken);
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
        return ValidateFile(filePath, format, CanOpenIniValidationOptions.Default, cancellationToken);
    }

    /// <summary>Validates an EDS or DCF file with severity overrides and optional reader and model passes.</summary>
    /// <param name="filePath">Path of the file to read.</param>
    /// <param name="format">INI dialect to validate; it is not inferred from the extension.</param>
    /// <param name="options">Severity overrides and other validation options.</param>
    /// <param name="cancellationToken">Token observed while scanning and validating the input.</param>
    /// <returns>All raw-source findings after overrides, ordered by source line, severity and rule code.</returns>
    public static IReadOnlyList<IniValidationIssue> ValidateFile(
        string filePath,
        CanOpenIniFormat format,
        CanOpenIniValidationOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNull(filePath);
        ThrowIfNull(options);

        using var reader = File.OpenText(filePath);
        return Validate(
            reader,
            filePath,
            format,
            options,
            () => format == CanOpenIniFormat.Dcf
                ? LibraryPass.Dcf(CanOpenFile.Dcf.ReadFileWithDiagnostics(filePath))
                : LibraryPass.Eds(CanOpenFile.Eds.ReadFileWithDiagnostics(filePath)),
            cancellationToken);
    }

    private static List<IniValidationIssue> Validate(
        TextReader reader,
        string sourceName,
        CanOpenIniFormat format,
        CanOpenIniValidationOptions options,
        Func<(IReadOnlyList<ParseDiagnostic>, Func<IReadOnlyList<ValidationIssue>>)> libraryRead,
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

        if (options.IncludeReaderDiagnostics || options.IncludeModelValidation)
        {
            LibraryPass.Run(libraryRead, sourceName, options, findings);
            cancellationToken.ThrowIfCancellationRequested();
        }

        IEnumerable<IniValidationIssue> result = findings;
        if (options.SeverityOverrides is { Count: > 0 } overrides)
        {
            result = ApplyOverrides(findings, overrides);
        }

        return result
            .Distinct()
            .OrderBy(f => f.Line ?? 0)
            .ThenByDescending(f => f.Severity)
            .ThenBy(f => f.Code, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<IniValidationIssue> ApplyOverrides(
        List<IniValidationIssue> findings,
        IReadOnlyDictionary<string, IniValidationSeverity?> overrides)
    {
        var lookup = new Dictionary<string, IniValidationSeverity?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in overrides)
        {
            lookup[pair.Key] = pair.Value;
        }

        foreach (var finding in findings)
        {
            if (!lookup.TryGetValue(finding.Code, out var severity))
            {
                yield return finding;
            }
            else if (severity is { } replaced)
            {
                yield return finding with { Severity = replaced };
            }
        }
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
