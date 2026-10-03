namespace EdsDcfNet.Models;

/// <summary>
/// Represents the [DynamicChannels] section describing dynamic network variable segments
/// for CiA 302-4 programmable devices.
/// </summary>
public class DynamicChannels
{
    /// <summary>
    /// List of dynamic channel segments.
    /// </summary>
    public List<DynamicChannelSegment> Segments { get; } = new();

    /// <summary>
    /// Entries of the <c>[DynamicChannels]</c> section that the reader does not map onto a
    /// property, in file order. Keys compare case-insensitively.
    /// </summary>
    /// <remarks>
    /// CiA 306-1 allows additional entries inside the standard sections "in order to support
    /// future extensions" (§ 6.2). The EDS/DCF writers emit these entries after the keys they
    /// generate for the section. A key the writer already generates for this section is not
    /// written a second time. A section with <c>NrOfSeg=0</c> and unknown entries is read as an instance without segments so these entries are kept.
    /// </remarks>
    public OrderedStringDictionary RemainingEntries { get; } = new();
}

/// <summary>
/// Represents a single segment within the [DynamicChannels] section.
/// </summary>
/// <remarks>
/// <para>
/// EDS and DCF (CiA 306-3 §5.2.2) store <c>Type</c>, <c>Dir</c>, <c>Range</c>, and
/// <c>PPOffset</c>. XDD and XDC (CiA 311 <c>dynamicChannel</c>) store the same segment
/// with these attributes:
/// </para>
/// <list type="table">
/// <listheader>
/// <term>EDS/DCF</term>
/// <description>XDD/XDC</description>
/// </listheader>
/// <item>
/// <term><c>Type</c> (Unsigned16)</term>
/// <description><c>dataType</c> (<c>xsd:hexBinary</c>). The schema annotation specifies two hex digits.</description>
/// </item>
/// <item>
/// <term><c>Dir</c> (<c>ro</c>, <c>wo</c>, <c>rw</c>, <c>rwr</c>, <c>rww</c>, <c>const</c>)</term>
/// <description><c>accessType</c> (<c>readOnly</c>, <c>writeOnly</c>, <c>readWriteOutput</c>).</description>
/// </item>
/// <item>
/// <term><c>Range</c> (<c>start-end</c> or one index)</term>
/// <description><c>startIndex</c> and <c>endIndex</c> (<c>xsd:hexBinary</c>). Both attributes are required.</description>
/// </item>
/// <item>
/// <term>No EDS/DCF key. Derived from <c>Range</c> when this property is unset.</term>
/// <description><c>maxNumber</c> (<c>xsd:unsignedInt</c>, required).</description>
/// </item>
/// <item>
/// <term><c>PPOffset</c> (first value only)</term>
/// <description><c>addressOffset</c> (<c>xsd:hexBinary</c>, required). Process-image offset (CiA 311 Table 51).</description>
/// </item>
/// <item>
/// <term>No EDS/DCF key.</term>
/// <description><c>bitAlignment</c> (<c>xsd:unsignedByte</c>, optional).</description>
/// </item>
/// </list>
/// <para>
/// CiA 306-3 also allows <c>PPOffset = offset[, addressDifference]</c> for BOOLEAN
/// segments. Only <c>offset</c> is stored in <see cref="PPOffset"/>. The address
/// difference is not this offset and is not mapped to <c>addressOffset</c>.
/// </para>
/// </remarks>
public class DynamicChannelSegment
{
    /// <summary>
    /// Data type index for this segment (Unsigned16).
    /// </summary>
    /// <remarks>
    /// Written as <c>dynamicChannel/@dataType</c>. Values <c>0</c> through <c>255</c>
    /// use two uppercase hex digits. Larger values use four digits so the full
    /// <see cref="ushort"/> is kept; <c>xsd:hexBinary</c> allows that width.
    /// </remarks>
    public ushort Type { get; set; }

    /// <summary>
    /// Direction/access type for this segment (for example <c>ro</c>, <c>rww</c>, <c>rwr</c>).
    /// </summary>
    /// <remarks>
    /// XDD/XDC <c>accessType</c> allows <c>readOnly</c>, <c>writeOnly</c>, and
    /// <c>readWriteOutput</c>. On write, <see cref="AccessType.ReadOnly"/> and
    /// <see cref="AccessType.Constant"/> become <c>readOnly</c>,
    /// <see cref="AccessType.WriteOnly"/> becomes <c>writeOnly</c>, and
    /// <see cref="AccessType.ReadWrite"/>, <see cref="AccessType.ReadWriteInput"/>,
    /// and <see cref="AccessType.ReadWriteOutput"/> become <c>readWriteOutput</c>
    /// (the only read/write token in the schema).
    /// </remarks>
    public AccessType Dir { get; set; }

    /// <summary>
    /// Index range for this segment (for example "0xA080-0xA0BF").
    /// </summary>
    /// <remarks>
    /// EDS/DCF keep this text. XDD/XDC split it into <c>startIndex</c> and
    /// <c>endIndex</c>. A single index is written as both attributes because
    /// <c>endIndex</c> is required. A <c>0x</c> prefix is omitted so the attributes
    /// are <c>xsd:hexBinary</c>.
    /// </remarks>
    public string Range { get; set; } = string.Empty;

    /// <summary>
    /// Process image offset for this segment (Unsigned32).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is CiA 306-3 §5.2.2 <c>PPOffset</c> and CiA 311 Table 51
    /// <c>addressOffset</c>: the offset of the segment inside the process image.
    /// It is not the optional BOOLEAN address difference from the EDS tuple
    /// <c>offset, addressDifference</c>.
    /// </para>
    /// <para>
    /// <c>addressOffset</c> is <c>xsd:hexBinary</c> with no fixed length, so
    /// <c>0010</c> and <c>10</c> are different values. The XDD/XDC reader keeps
    /// the spelling internally and the writer emits it while this property still
    /// equals the value read. Otherwise the writer formats this property with an
    /// even digit count, uppercase letters, and at least four digits.
    /// </para>
    /// </remarks>
    public uint PPOffset { get; set; }

    /// <summary>
    /// Maximum number of objects that can be allocated in this segment
    /// (<c>xsd:unsignedInt</c>), or <see langword="null"/> when the file did not
    /// supply <c>maxNumber</c>.
    /// </summary>
    /// <remarks>
    /// EDS and DCF have no equivalent key, so segments from those formats leave
    /// this unset. The XDD/XDC writer then derives the required attribute from
    /// <see cref="Range"/>: the inclusive index span (<c>end - start + 1</c>),
    /// <c>1</c> when the range names a single index, or <c>0</c> when the range
    /// cannot be parsed or the end index is below the start index. A value read
    /// from XDD or XDC is written unchanged, including when it is smaller than
    /// that span. The writer does not assign the derived number back to this property.
    /// </remarks>
    public uint? MaxNumber { get; set; }

    /// <summary>
    /// Bit alignment of the segment inside the process image
    /// (<c>xsd:unsignedByte</c>), or <see langword="null"/> when the optional
    /// attribute is absent.
    /// </summary>
    /// <remarks>
    /// Typical values are 1 (bit), 8 (byte), 16 (word), and 32 (double word).
    /// EDS and DCF do not define this attribute. The XDD/XDC writer emits it
    /// only when this property has a value, including <c>0</c>.
    /// </remarks>
    public byte? BitAlignment { get; set; }

    /// <summary>
    /// Original XDD/XDC <c>addressOffset</c> text. The writer emits it only while
    /// <see cref="PPOffset"/> still equals <see cref="AddressOffsetLexicalBaseline"/>.
    /// </summary>
    internal string? AddressOffsetLexical { get; set; }

    /// <summary>
    /// <see cref="PPOffset"/> captured when <see cref="AddressOffsetLexical"/> was stored.
    /// </summary>
    /// <remarks>
    /// A schema-valid <c>hexBinary</c> value that does not fit in
    /// <see cref="PPOffset"/> leaves the property at <c>0</c> and stores that
    /// <c>0</c> here, so the original text is kept until the caller changes the offset.
    /// </remarks>
    internal uint AddressOffsetLexicalBaseline { get; set; }
}
