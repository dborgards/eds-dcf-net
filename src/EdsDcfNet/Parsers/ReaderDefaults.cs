namespace EdsDcfNet.Parsers;

/// <summary>
/// Format-neutral default values shared by all reader implementations.
/// </summary>
public static class ReaderDefaults
{
    /// <summary>
    /// Default maximum input size (10 MB) used by readers to guard
    /// against unbounded memory consumption.
    /// </summary>
    public const long DefaultMaxInputSize = 10L * 1024 * 1024;
}
