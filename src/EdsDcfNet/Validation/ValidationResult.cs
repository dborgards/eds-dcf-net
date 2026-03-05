namespace EdsDcfNet.Validation;

using System.Globalization;

/// <summary>
/// Represents the result of validating a CANopen model.
/// Contains a list of zero or more <see cref="ValidationError"/> entries.
/// </summary>
public class ValidationResult
{
    private readonly List<ValidationError> _errors = new();

    /// <summary>
    /// All validation errors found.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors => _errors;

    /// <summary>
    /// <see langword="true"/> when no validation errors were found.
    /// </summary>
    public bool IsValid => _errors.Count == 0;

    internal void AddError(string code, string message, string? context = null)
    {
        _errors.Add(new ValidationError(code, message, context));
    }
}

/// <summary>
/// Describes a single validation error with a machine-readable code and human-readable message.
/// </summary>
public sealed class ValidationError
{
    /// <summary>
    /// Creates a new validation error.
    /// </summary>
    /// <param name="code">Machine-readable error code (e.g. "NODE_ID_RANGE").</param>
    /// <param name="message">Human-readable description of the problem.</param>
    /// <param name="context">Optional context path (e.g. section or object index).</param>
    public ValidationError(string code, string message, string? context = null)
    {
        Code = code;
        Message = message;
        Context = context;
    }

    /// <summary>
    /// Machine-readable error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Optional context path (e.g. "[DeviceCommissioning]" or "[1000]").
    /// </summary>
    public string? Context { get; }

    /// <inheritdoc/>
    public override string ToString()
    {
        return Context != null
            ? string.Format(CultureInfo.InvariantCulture, "{0}: {1} ({2})", Context, Message, Code)
            : string.Format(CultureInfo.InvariantCulture, "{0} ({1})", Message, Code);
    }
}
