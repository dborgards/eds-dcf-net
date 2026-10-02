namespace EdsDcfNet.Validation;

/// <summary>
/// Stable machine-readable identifiers for <see cref="ValidationIssue.Code"/>.
/// </summary>
public static class ValidationIssueCodes
{
    /// <summary>
    /// INI section name, key, or value that would not survive reading the written file back.
    /// Covers C0 controls, a tab outside a value, leading or trailing whitespace, and separators
    /// the INI parser consumes (<c>=</c>, <c>[</c>, <c>]</c>, and a key that starts with <c>;</c>).
    /// </summary>
    public const string IniTextNotRoundTrippable = "INI_TEXT_NOT_ROUND_TRIPPABLE";
}
