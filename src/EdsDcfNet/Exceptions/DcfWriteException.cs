namespace EdsDcfNet.Exceptions;

/// <summary>
/// Exception thrown when writing a DCF file fails.
/// </summary>
public class DcfWriteException : Exception
{
    /// <summary>
    /// Section name where the error occurred (if applicable).
    /// </summary>
    public string? SectionName { get; set; }

    public DcfWriteException()
    {
    }

    public DcfWriteException(string message) : base(message)
    {
    }

    public DcfWriteException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public DcfWriteException(string message, string sectionName) : base(message)
    {
        SectionName = sectionName;
    }
}
