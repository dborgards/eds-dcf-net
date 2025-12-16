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

    public EdsParseException()
    {
    }

    public EdsParseException(string message) : base(message)
    {
    }

    public EdsParseException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public EdsParseException(string message, int lineNumber) : base(message)
    {
        LineNumber = lineNumber;
    }

    public EdsParseException(string message, string sectionName, int lineNumber) : base(message)
    {
        SectionName = sectionName;
        LineNumber = lineNumber;
    }
}
