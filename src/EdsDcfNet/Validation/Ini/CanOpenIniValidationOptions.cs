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
}
