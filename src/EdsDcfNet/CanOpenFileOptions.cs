namespace EdsDcfNet;

using System.Text;
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
    /// <item><description>Malformed INI section headers such as <c>[2000</c> or text after the closing bracket other than a <c>;</c> comment (default: ignore the header and the keys that follow it up to the next valid header)</description></item>
    /// <item><description>INI lines without <c>=</c> or with an empty key (default: ignore the line). A line starting with <c>#</c> without <c>=</c> is ignored in both modes</description></item>
    /// <item><description>Duplicate INI section headers (default: merge the keys into the earlier section)</description></item>
    /// <item><description>Unknown XDD/XDC baud-rate strings on <c>supportedBaudRate</c>, <c>actualBaudRate</c>, and <c>baudRate/@defaultValue</c> (default: treat as 0 / ignore)</description></item>
    /// <item><description>Unknown boolean tokens in <c>ValueConverter.ParseBoolean</c> (default: treat as <see langword="false"/>)</description></item>
    /// <item><description>Unknown access-type tokens in <c>ValueConverter.ParseAccessType</c> (default: <c>ro</c>)</description></item>
    /// <item><description>
    /// Malformed EDS/DCF numeric keys on objects and sub-objects:
    /// <c>ObjectType</c> (default: VAR / <c>0x7</c>),
    /// object <c>DataType</c> (default: left unset),
    /// sub-object <c>DataType</c> (default: <c>0</c>),
    /// <c>SubNumber</c> and <c>CompactSubObj</c> (default: left unset),
    /// and <c>ObjFlags</c> (default: <c>0</c>).
    /// Object-list counts (<c>SupportedObjects</c>, <c>ObjectLinks</c>, module <c>NrOfEntries</c>)
    /// default to <c>0</c>; a malformed index entry is skipped and the rest of the list is read.
    /// The object itself is kept. Strict mode throws <see cref="Exceptions.EdsParseException"/>
    /// with the same <see cref="Diagnostics.ParseDiagnostic.Code"/>.
    /// </description></item>
    /// <item><description>
    /// Malformed numeric keys of the EDS/DCF <c>[DeviceInfo]</c> section (CiA 306-1 § 6.5):
    /// <c>VendorNumber</c>, <c>ProductNumber</c>, <c>RevisionNumber</c>, <c>Granularity</c>,
    /// <c>DynamicChannelsSupported</c>, <c>NrOfRXPDO</c>, <c>NrOfTXPDO</c> and <c>CompactPDO</c>
    /// (default: the value of an absent key, <c>0</c>, or <c>8</c> for <c>Granularity</c>).
    /// </description></item>
    /// <item><description>
    /// Malformed numeric keys of the DCF <c>[DeviceComissioning]</c> section (CiA 306-1 § 7.3.5):
    /// <c>NodeID</c> (default: <c>1</c>), <c>Baudrate</c> (default: <c>250</c>),
    /// <c>NetNumber</c> (default: <c>0</c>) and <c>LSS_SerialNumber</c> (default: left unset).
    /// A readable <c>NodeID</c> outside <c>1..127</c> is kept in default mode and reported as a
    /// diagnostic and by validation; strict mode throws. When a DCF has both
    /// <c>[DeviceComissioning]</c> and <c>[DeviceCommissioning]</c>, the normative first spelling is read
    /// and the second section is kept unchanged in <c>AdditionalSections</c> with a diagnostic
    /// (strict: throw).
    /// </description></item>
    /// <item><description>
    /// Malformed <c>[DynamicChannels]</c> keys <c>NrOfSeg</c> (default: <c>0</c>), <c>Type&lt;n&gt;</c>
    /// (default: <c>0</c>) and <c>PPOffset&lt;n&gt;</c> (default: offset <c>0</c> without an address
    /// difference)
    /// </description></item>
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
    /// Malformed XDD/XDC unsigned numeric attributes such as <c>subNumber</c>,
    /// <c>dynamicChannel</c> <c>maxNumber</c> and <c>bitAlignment</c>, general-feature
    /// counts, and <c>networkNumber</c>
    /// (default: ignore / leave unset; surrounding whitespace and optional leading sign are accepted)
    /// </description></item>
    /// <item><description>
    /// XDD/XDC <c>dynamicChannel</c>. Schema <c>accessType</c> values are
    /// <c>readOnly</c>, <c>writeOnly</c>, and <c>readWriteOutput</c>. Lenient mode also
    /// accepts the EDS short forms <c>ro</c>, <c>wo</c>, <c>rw</c>, <c>rwr</c>, <c>rww</c>,
    /// and <c>const</c>; strict mode rejects them.
    /// <c>addressOffset</c> is <c>xsd:hexBinary</c> with no fixed length. The original
    /// spelling is kept for writing while <c>PPOffset</c> still matches it. A schema-valid
    /// value that does not fit in 32 bits is reported and left at <c>0</c> in both modes
    /// (strict does not throw), and the original text is kept until <c>PPOffset</c> changes.
    /// An odd number of hex digits, a <c>0x</c> prefix, or a non-hex character is a parse
    /// error (lenient: ignore; strict: throw).
    /// <c>pDOmappingIndex</c> is a legacy attribute written by older versions of this
    /// library. When <c>addressOffset</c> is absent, lenient mode copies a numeric value
    /// to <c>PPOffset</c> and reports a diagnostic; strict mode throws.
    /// </description></item>
    /// <item><description>
    /// XDD/XDC <c>objFlags</c> is <c>xsd:hexBinary</c> (CiA 311 Annex A.1.4, four hex digits;
    /// bits 0..2 defined, bits 3..31 reserved). Surrounding whitespace is trimmed. An odd
    /// number of hex digits is accepted in lenient mode with a diagnostic and rejected in
    /// strict mode. A schema-valid value that does not fit in 32 bits is reported and left
    /// at <c>0</c> in both modes, and the original text is kept for writing until
    /// <c>ObjFlags</c> changes. Reserved bits 3..31 are reported and still stored. A leading
    /// sign or a <c>0x</c> prefix is not hexadecimal (default: ignore; strict: throw).
    /// </description></item>
    /// <item><description>
    /// Unknown CPJ <c>NodeNPresent</c> tokens in <c>ValueConverter.ParsePresentFlag</c>
    /// (default: treat as not present / <see langword="false"/>)
    /// </description></item>
    /// </list>
    /// <para>
    /// Not covered: a malformed numeric count or version key of the <c>[Comments]</c>
    /// (<c>Lines</c>), <c>[Tools]</c> (<c>Items</c>), <c>[SupportedModules]</c> /
    /// <c>[ConnectedModules]</c> (<c>NrOfEntries</c>) and module (<c>ProductVersion</c>,
    /// <c>ProductRevision</c>, <c>Lines</c>) sections throws <see cref="Exceptions.EdsParseException"/>
    /// in both modes.
    /// </para>
    /// </remarks>
    public bool StrictParsing { get; init; }

    /// <summary>
    /// Gets the encoding used to decode EDS, DCF, CPJ, XDD, and XDC bytes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see langword="null"/> (the default) selects automatic detection for EDS, DCF, and CPJ.
    /// A byte-order mark selects UTF-8, UTF-16, or UTF-32 and is not returned as text.
    /// Otherwise the buffered bytes are decoded as strict UTF-8 (<c>throwOnInvalidBytes</c>).
    /// When that fails with <see cref="DecoderFallbackException"/>, the bytes passed to that
    /// strict decode are decoded as ISO-8859-1. A leading UTF-8 byte-order mark is excluded
    /// from both decodes. A diagnostic is reported
    /// (<see cref="Diagnostics.ParseDiagnosticCodes.IniDecodedAsIso88591"/>,
    /// &quot;file is not valid UTF-8, decoded as ISO-8859-1&quot;). The bytes are buffered
    /// once, including from a non-seekable stream, and decoded from that buffer.
    /// </para>
    /// <para>
    /// For XDD and XDC, <see langword="null"/> follows a byte-order mark when one is present
    /// (UTF-8, UTF-16, or UTF-32). Otherwise the encoding named by the XML declaration is
    /// used, and UTF-8 is used when the declaration does not name one. A declared name that
    /// this runtime cannot create fails the read with <see cref="Exceptions.EdsParseException"/>;
    /// the message includes that name. Invalid byte sequences are not replaced with U+FFFD.
    /// </para>
    /// <para>
    /// An explicit value is used as supplied and does not fall back to ISO-8859-1. On XDD and
    /// XDC it overrides the XML declaration. A byte-order mark for that encoding is still
    /// removed, including when the encoding instance was constructed not to emit a preamble.
    /// String overloads are already decoded text and ignore this property.
    /// </para>
    /// </remarks>
    public Encoding? Encoding { get; init; }

    internal static long ResolveMaxInputSize(CanOpenFileOptions? options)
        => options?.MaxInputSize ?? ReaderDefaults.DefaultMaxInputSize;

    internal static bool ResolveStrictParsing(CanOpenFileOptions? options)
        => options?.StrictParsing ?? false;

    internal static Encoding? ResolveEncoding(CanOpenFileOptions? options)
        => options?.Encoding;
}
