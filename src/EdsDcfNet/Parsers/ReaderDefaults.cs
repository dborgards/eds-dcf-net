namespace EdsDcfNet.Parsers;

/// <summary>
/// Format-neutral default values shared by all reader implementations.
/// </summary>
public static class ReaderDefaults
{
    /// <summary>
    /// Default maximum input size used by readers to guard against unbounded
    /// memory consumption. The value is interpreted as a limit on the number
    /// of bytes for raw input sources (file or stream size) and as a limit
    /// on the number of decoded characters for text-based APIs
    /// (<c>ReadStream</c> or <c>ReadString</c>). The current value
    /// corresponds to 10 MiB.
    /// </summary>
    public const long DefaultMaxInputSize = 10L * 1024 * 1024;
}
