namespace EdsDcfNet.Validation;

/// <summary>Options for <see cref="CanOpenIniValidator"/>.</summary>
public sealed class CanOpenIniValidationOptions
{
    /// <summary>Gets options without overrides; validation reports what the overloads without options report.</summary>
    public static CanOpenIniValidationOptions Default { get; } = new();

    /// <summary>
    /// Gets per-rule severity overrides, keyed by rule id (case-insensitive). A value replaces the
    /// severity of every issue of that rule; <see langword="null"/> turns the rule off.
    /// An id that no issue uses has no effect.
    /// </summary>
    public IReadOnlyDictionary<string, IniValidationSeverity?> SeverityOverrides { get; init; } =
        new Dictionary<string, IniValidationSeverity?>();

    /// <summary>
    /// Gets a value indicating whether the file is also loaded with the EdsDcfNet reader and its
    /// <see cref="Diagnostics.ParseDiagnostic"/>s are reported with their own code
    /// (for example <c>INVALID_NODE_ID</c>). A diagnostic without a code is reported as
    /// <c>LIB001</c>, a reader that throws as <c>LIB002</c>. Reader errors stay errors; warnings
    /// and infos become warnings. Default <see langword="false"/>.
    /// </summary>
    public bool IncludeReaderDiagnostics { get; init; }

    /// <summary>
    /// Gets a value indicating whether the loaded model is also checked by
    /// <see cref="CanOpenModelValidator"/> with default options. Issues use
    /// <see cref="ValidationIssue.Code"/>, or <c>LIB003</c> when they have none. Issues on
    /// <c>ObjectDictionary.*</c> paths are not reported: the raw rules cover the object
    /// dictionary in more detail and with line numbers. A reader that throws is reported as
    /// <c>LIB002</c>. Default <see langword="false"/>.
    /// </summary>
    public bool IncludeModelValidation { get; init; }
}
