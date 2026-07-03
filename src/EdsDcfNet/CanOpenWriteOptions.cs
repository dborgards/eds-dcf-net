namespace EdsDcfNet;

/// <summary>
/// Optional behavior for <see cref="CanOpenFile"/> and format-specific operations
/// (<see cref="CanOpenFile.Eds"/>, <see cref="CanOpenFile.Dcf"/>, <see cref="CanOpenFile.Cpj"/>,
/// <see cref="CanOpenFile.Xdd"/>, <see cref="CanOpenFile.Xdc"/>) write methods.
/// </summary>
/// <remarks>
/// This type intentionally holds only cross-format write concerns. Format-specific
/// options are introduced as derived per-format option types (unsealing this type
/// on demand) rather than as additional properties here — see the
/// "Options extension pattern" section in the README.
/// </remarks>
public sealed class CanOpenWriteOptions
{
    /// <summary>
    /// Gets a default options instance with validation disabled.
    /// </summary>
    public static CanOpenWriteOptions Default { get; } = new();

    /// <summary>
    /// Gets an options instance that validates the model before writing.
    /// </summary>
    public static CanOpenWriteOptions Validated { get; } = new() { ValidateBeforeWrite = true };

    /// <summary>
    /// When <see langword="true"/>, write methods validate the model and throw
    /// <see cref="Exceptions.ModelValidationException"/> when validation issues are found.
    /// Default is <see langword="false"/> for backward compatibility.
    /// </summary>
    public bool ValidateBeforeWrite { get; init; }
}
