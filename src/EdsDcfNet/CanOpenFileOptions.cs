namespace EdsDcfNet;

using EdsDcfNet.Parsers;

/// <summary>
/// Shared options for <see cref="CanOpenFile"/> and format-specific operation entry points.
/// </summary>
/// <remarks>
/// Prefer passing a single options instance instead of adding new overload parameters
/// to the <see cref="CanOpenFile"/> facade. New formats should expose dedicated
/// operation classes (for example <see cref="EdsCanOpenOperations"/>) that accept
/// this type. This type intentionally holds only cross-format read concerns;
/// format-specific options are introduced as derived per-format option types
/// (unsealing this type on demand) — see the "Options extension pattern" section
/// in the README.
/// </remarks>
public sealed class CanOpenFileOptions
{
    /// <summary>
    /// Gets the default options (10 MiB input limit).
    /// </summary>
    public static CanOpenFileOptions Default { get; } = new();

    /// <summary>
    /// Maximum input size for read operations. For file-path APIs the value is
    /// compared against file size in bytes; for stream and string APIs it is
    /// compared against decoded character count.
    /// </summary>
    public long MaxInputSize { get; init; } = ReaderDefaults.DefaultMaxInputSize;

    internal static long ResolveMaxInputSize(CanOpenFileOptions? options)
        => options?.MaxInputSize ?? ReaderDefaults.DefaultMaxInputSize;
}
