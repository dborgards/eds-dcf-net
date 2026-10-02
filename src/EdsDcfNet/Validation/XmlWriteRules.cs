namespace EdsDcfNet.Validation;

/// <summary>
/// Format-family checks applied after shared model validation when an XDD or XDC file is
/// written with <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/>.
/// </summary>
/// <remarks>
/// No rules yet. WP-18, WP-20, and WP-22 add XDD/XDC checks here so they do not apply to
/// EDS, DCF, or CPJ writes.
/// </remarks>
internal static class XmlWriteRules
{
    internal static void Apply(object model, List<ValidationIssue> issues)
    {
        _ = model;
        _ = issues;
    }
}
