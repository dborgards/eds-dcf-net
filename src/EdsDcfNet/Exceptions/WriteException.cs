namespace EdsDcfNet.Exceptions;

/// <summary>
/// Base exception for all CANopen file write failures.
/// Format-specific subclasses (e.g. <see cref="EdsWriteException"/>,
/// <see cref="DcfWriteException"/>) allow callers to catch either a
/// specific format or any write error via this base type.
/// </summary>
public abstract class WriteException : Exception
{
    /// <summary>
    /// Section or element context where the error occurred (if applicable).
    /// </summary>
    public string? SectionName { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WriteException"/> class.
    /// </summary>
    protected WriteException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WriteException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    protected WriteException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WriteException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    protected WriteException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WriteException"/> class with a specified error message and section name.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="sectionName">The section or element name where the error occurred.</param>
    protected WriteException(string message, string sectionName) : base(message)
    {
        SectionName = sectionName;
    }
}
