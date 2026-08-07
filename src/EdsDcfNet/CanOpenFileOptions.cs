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

    /// <summary>
    /// When <see langword="true"/>, silent parse fallbacks fail with
    /// <see cref="Exceptions.EdsParseException"/> instead of coercing to defaults.
    /// </summary>
    /// <remarks>
    /// <para>Default is <see langword="false"/> (lenient), matching real-world EDS/DCF tolerance.</para>
    /// <para>
    /// Enforced when reading through <see cref="CanOpenFile"/> format entry points
    /// (or legacy facade overloads that accept this options type). Direct
    /// <c>*Reader</c> APIs that do not take <see cref="CanOpenFileOptions"/> remain
    /// lenient; there is no public API to enable strict parsing on those readers.
    /// </para>
    /// <para>Currently enforced for:</para>
    /// <list type="bullet">
    /// <item><description>Duplicate keys within an INI section (default: last write wins)</description></item>
    /// <item><description>Unknown XDD/XDC baud-rate strings on <c>supportedBaudRate</c>, <c>actualBaudRate</c>, and <c>baudRate/@defaultValue</c> (default: treat as 0 / ignore)</description></item>
    /// <item><description>Unknown boolean tokens in <c>ValueConverter.ParseBoolean</c> (default: treat as <see langword="false"/>)</description></item>
    /// <item><description>Unknown access-type tokens in <c>ValueConverter.ParseAccessType</c> (default: <c>ro</c>)</description></item>
    /// <item><description>
    /// EDS/DCF <c>[FileInfo] FileVersion</c> / <c>FileRevision</c> and XDD/XDC <c>fileVersion</c>
    /// major/minor tooling forms such as <c>1.0</c> / <c>1,0</c> (default: accept major component;
    /// strict: require a plain <c>Unsigned8</c> integer). Malformed tokens throw with
    /// section/key (or <c>ProfileBody fileVersion</c>) attribution in both modes.
    /// </description></item>
    /// </list>
    /// <para>
    /// Residual lenient sites (tracked separately): XDD <c>ParseXddAccessType</c> /
    /// <c>ParseXmlBool</c> (#463), missing XDD <c>index</c>/<c>objectType</c> (#464),
    /// malformed XDD numeric attributes (#465), CPJ <c>ParsePresentFlag</c> (#466),
    /// and EDS/DCF vs XDD zero-padded <c>FileVersion</c> decimal alignment (#467).
    /// </para>
    /// </remarks>
    public bool StrictParsing { get; init; }

    internal static long ResolveMaxInputSize(CanOpenFileOptions? options)
        => options?.MaxInputSize ?? ReaderDefaults.DefaultMaxInputSize;

    internal static bool ResolveStrictParsing(CanOpenFileOptions? options)
        => options?.StrictParsing ?? false;
}
