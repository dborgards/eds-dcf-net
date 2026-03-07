namespace EdsDcfNet.Exceptions;

/// <summary>
/// Exception thrown when writing an EDS file fails.
/// </summary>
public class EdsWriteException : WriteException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EdsWriteException"/> class.
    /// </summary>
    public EdsWriteException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EdsWriteException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public EdsWriteException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EdsWriteException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public EdsWriteException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EdsWriteException"/> class with a specified error message and section name.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="sectionName">The section name where the error occurred.</param>
    public EdsWriteException(string message, string sectionName) : base(message, sectionName)
    {
    }
}
