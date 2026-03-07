namespace EdsDcfNet.Parsers;

/// <summary>
/// Format-neutral default values shared by all reader implementations.
/// </summary>
public static class ReaderDefaults
{
    /// <summary>
    /// Default maximum input size used by readers to guard against unbounded
    /// memory consumption. For file-path APIs the value is compared against
    /// the file size in bytes; for stream and string APIs
    /// (<c>ReadStream</c> / <c>ReadString</c>) it is compared against the
    /// number of decoded characters. The current value corresponds to
    /// 10 MiB.
    /// </summary>
    public const long DefaultMaxInputSize = 10L * 1024 * 1024;
}
