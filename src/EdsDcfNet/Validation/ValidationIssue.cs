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
    {
        Path = path;
        Message = message;
    }

    /// <summary>
    /// Gets the logical model path where the issue occurred.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the human-readable validation message.
    /// </summary>
    public string Message { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Path + ": " + Message;
    }
}
