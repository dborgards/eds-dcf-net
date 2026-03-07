namespace EdsDcfNet.Exceptions;

/// <summary>
/// Exception thrown when writing an XDC file fails.
/// </summary>
public class XdcWriteException : WriteException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XdcWriteException"/> class.
    /// </summary>
    public XdcWriteException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XdcWriteException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public XdcWriteException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XdcWriteException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public XdcWriteException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XdcWriteException"/> class with a specified error message and section name.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="sectionName">The section or element name where the error occurred.</param>
    public XdcWriteException(string message, string sectionName) : base(message, sectionName)
    {
    }
}
