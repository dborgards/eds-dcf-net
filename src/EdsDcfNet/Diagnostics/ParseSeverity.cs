namespace EdsDcfNet.Diagnostics;

/// <summary>
/// Severity of a <see cref="ParseDiagnostic"/> reported while reading a CANopen file.
/// </summary>
public enum ParseSeverity
{
    /// <summary>Informational note; no value was changed.</summary>
    Info = 0,

    /// <summary>The input deviated from the specification and a value was coerced or ignored.</summary>
    Warning = 1,

    /// <summary>A serious problem; the model may be incomplete.</summary>
    Error = 2,
}
