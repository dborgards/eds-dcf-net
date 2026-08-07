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
    /// Unknown XDD/XDC access-type tokens in <c>ParseXddAccessType</c> (default: <c>ro</c>)
    /// and unknown XML boolean tokens in <c>ParseXmlBool</c> (default: <see langword="false"/>)
    /// </description></item>
    /// <item><description>
    /// EDS/DCF <c>[FileInfo] FileVersion</c> / <c>FileRevision</c> and XDD/XDC <c>fileVersion</c>
    /// major/minor tooling forms such as <c>1.0</c> / <c>1,0</c> (default: accept major component;
    /// strict: require a plain <c>Unsigned8</c> integer). Malformed tokens throw with
    /// section/key (or <c>ProfileBody fileVersion</c>) attribution in both modes.
    /// Zero-padded values such as <c>010</c> parse as decimal <c>10</c> (aligned across EDS/DCF/XDD).
    /// </description></item>
    /// <item><description>
    /// XDD/XDC <c>CANopenObject</c> missing <c>index</c>
    /// (default: treat as <c>0x0000</c>). Sub-objects use <c>subIndex</c>, which
    /// remains lenient (missing → <c>00</c>) in both modes.
    /// </description></item>
    /// <item><description>
    /// XDD/XDC <c>CANopenObject</c> / <c>CANopenSubObject</c> missing or invalid
    /// <c>objectType</c> (default: <c>0x7</c> VAR). Schema-valid <c>xsd:unsignedByte</c>
    /// lexical forms (optional leading sign, surrounding whitespace) are accepted after trim.
    /// </description></item>
    /// <item><description>
    /// Malformed XDD/XDC unsigned numeric attributes such as <c>objFlags</c>, <c>subNumber</c>,
    /// <c>pDOmappingIndex</c>, general-feature counts, and <c>networkNumber</c>
    /// (default: ignore / leave unset; surrounding whitespace and optional leading sign are accepted)
    /// </description></item>
    /// <item><description>
    /// Unknown CPJ <c>NodeNPresent</c> tokens in <c>ValueConverter.ParsePresentFlag</c>
    /// (default: treat as not present / <see langword="false"/>)
    /// </description></item>
    /// </list>
    /// </remarks>
    public bool StrictParsing { get; init; }

    internal static long ResolveMaxInputSize(CanOpenFileOptions? options)
        => options?.MaxInputSize ?? ReaderDefaults.DefaultMaxInputSize;

    internal static bool ResolveStrictParsing(CanOpenFileOptions? options)
        => options?.StrictParsing ?? false;
}
