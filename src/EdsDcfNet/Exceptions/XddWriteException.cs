namespace EdsDcfNet.Exceptions;

/// <summary>
/// Exception thrown when writing an XDD file fails.
/// </summary>
public class XddWriteException : Exception
{
    /// <summary>
    /// Section or element context where the error occurred (if applicable).
    /// </summary>
    public string? SectionName { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="XddWriteException"/> class.
    /// </summary>
    public XddWriteException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XddWriteException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public XddWriteException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XddWriteException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public XddWriteException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XddWriteException"/> class with a specified error message and section name.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="sectionName">The section or element name where the error occurred.</param>
    public XddWriteException(string message, string sectionName) : base(message)
    {
        SectionName = sectionName;
    }
}
