namespace EdsDcfNet.Exceptions;

/// <summary>
/// Exception thrown when parsing an EDS file fails.
/// </summary>
public class EdsParseException : Exception
{
    /// <summary>
    /// Line number where the error occurred (if applicable).
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// Section name where the error occurred (if applicable).
    /// </summary>
    public string? SectionName { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EdsParseException"/> class.
    /// </summary>
    public EdsParseException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EdsParseException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public EdsParseException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EdsParseException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public EdsParseException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EdsParseException"/> class with a specified error message and line number.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="lineNumber">The line number where the error occurred.</param>
    public EdsParseException(string message, int lineNumber) : base(message)
    {
        LineNumber = lineNumber;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EdsParseException"/> class with a specified error message, section name, and line number.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="sectionName">The section name where the error occurred.</param>
    /// <param name="lineNumber">The line number where the error occurred.</param>
    public EdsParseException(string message, string sectionName, int lineNumber) : base(message)
    {
        SectionName = sectionName;
        LineNumber = lineNumber;
    }
}
