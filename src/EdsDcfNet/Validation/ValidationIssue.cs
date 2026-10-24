namespace EdsDcfNet.Validation;

/// <summary>
/// Represents a single model validation problem.
/// </summary>
public sealed class ValidationIssue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationIssue"/> class.
    /// </summary>
    /// <param name="path">Logical model path where the issue occurred.</param>
    /// <param name="message">Human-readable validation message.</param>
    public ValidationIssue(string path, string message)
        : this(path, message, code: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationIssue"/> class.
    /// </summary>
    /// <param name="path">Logical model path where the issue occurred.</param>
    /// <param name="message">Human-readable validation message.</param>
    /// <param name="code">
    /// Machine-readable issue code, typically a <see cref="ValidationIssueCodes"/> constant.
    /// <see langword="null"/> when the issue has no code.
    /// </param>
    public ValidationIssue(string path, string message, string? code)
        : this(path, message, code, ValidationSeverity.Error)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationIssue"/> class with a severity.
    /// </summary>
    /// <param name="path">Logical model path where the issue occurred.</param>
    /// <param name="message">Human-readable validation message.</param>
    /// <param name="code">Machine-readable issue code, or <see langword="null"/>.</param>
    /// <param name="severity">Severity of the issue.</param>
    public ValidationIssue(string path, string message, string? code, ValidationSeverity severity)
    {
        Path = path;
        Message = message;
        Code = code;
        Severity = severity;
    }

    /// <summary>
    /// Gets the logical model path where the issue occurred.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the human-readable validation message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the machine-readable issue code, or <see langword="null"/> when the issue was created without one.
    /// </summary>
    public string? Code { get; }

    /// <summary>Gets the severity. Existing constructors produce <see cref="ValidationSeverity.Error"/>.</summary>
    public ValidationSeverity Severity { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Path + ": " + Message;
    }
}
