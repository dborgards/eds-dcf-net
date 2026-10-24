namespace EdsDcfNet.Validation;

/// <summary>Severity of a <see cref="ValidationIssue"/>.</summary>
public enum ValidationSeverity
{
    /// <summary>A conformance error. This is the default for backward compatibility.</summary>
    Error,

    /// <summary>A conformance warning.</summary>
    Warning,
}
