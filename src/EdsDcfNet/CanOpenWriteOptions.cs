namespace EdsDcfNet;

/// <summary>
/// Optional behavior for <see cref="CanOpenFile"/> write operations.
/// </summary>
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
