namespace EdsDcfNet.Exceptions;

/// <summary>
/// Exception thrown when writing a CPJ file fails.
/// </summary>
public class CpjWriteException : WriteException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CpjWriteException"/> class.
    /// </summary>
    public CpjWriteException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CpjWriteException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public CpjWriteException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CpjWriteException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CpjWriteException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CpjWriteException"/> class with a specified error message and section name.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="sectionName">The section name where the error occurred.</param>
    public CpjWriteException(string message, string sectionName) : base(message, sectionName)
    {
    }
}
