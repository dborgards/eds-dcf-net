namespace EdsDcfNet.Validation;

/// <summary>Describes one rule reported by <see cref="CanOpenIniValidator"/>.</summary>
/// <param name="Id">Stable rule code (for example <c>VAL001</c>), as used in <see cref="IniValidationIssue.Code"/>.</param>
/// <param name="DefaultSeverity">
/// Severity the rule reports without overrides. Rules that report both severities list the
/// higher one; the issue itself always carries the severity that applies.
/// </param>
/// <param name="Title">Short description of what the rule checks.</param>
/// <param name="SpecReference">CiA specification reference, when the rule follows one directly.</param>
/// <param name="HelpUri">Optional link to further documentation.</param>
public sealed record ValidationRule(
    string Id,
    IniValidationSeverity DefaultSeverity,
    string Title,
    string? SpecReference,
    Uri? HelpUri);
