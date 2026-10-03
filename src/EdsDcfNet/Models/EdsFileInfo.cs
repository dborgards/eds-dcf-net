namespace EdsDcfNet.Models;

/// <summary>
/// Represents the [FileInfo] section of an EDS/DCF file.
/// Contains metadata about the file itself.
/// </summary>
public class EdsFileInfo
{
    /// <summary>
    /// File name according to OS restrictions.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Actual file version (CiA 306 <c>Unsigned8</c> integer).
    /// </summary>
    /// <remarks>
    /// On read, EDS/DCF parsers accept plain <em>decimal</em> integers (zero-padded
    /// <c>010</c> → 10, matching XDD <c>fileVersion</c>; not CiA octal) and — unless
    /// <see cref="CanOpenFileOptions.StrictParsing"/> is enabled — major/minor
    /// tooling forms such as <c>1.0</c> or <c>1,0</c> (major component only).
    /// </remarks>
    public byte FileVersion { get; set; } = 1;

    /// <summary>
    /// Original <c>fileVersion</c> text of an XDD/XDC read, or text to write there.
    /// </summary>
    /// <remarks>
    /// CiA 311 declares <c>fileVersion</c> as a free <c>xsd:string</c> (for example <c>vendor-r7</c> or
    /// <c>1.0</c>), which <see cref="FileVersion"/> cannot hold. The XDD/XDC reader keeps the text here
    /// when it is not the plain decimal spelling of <see cref="FileVersion"/>; <see cref="FileVersion"/>
    /// then holds the major component, or <c>1</c> when no number can be derived. The XDD/XDC writers emit
    /// the text while <see cref="FileVersion"/> is unchanged since the read; once the caller changes
    /// <see cref="FileVersion"/>, the number is written instead. A text assigned to a model that was not
    /// read from XDD/XDC is always written. EDS and DCF writers always use <see cref="FileVersion"/>.
    /// </remarks>
    public string? FileVersionText { get; set; }

    /// <summary><see cref="FileVersion"/> captured when <see cref="FileVersionText"/> was read.</summary>
    internal byte? FileVersionTextBaseline { get; set; }

    /// <summary>
    /// Actual file revision (CiA 306 <c>Unsigned8</c> integer).
    /// </summary>
    /// <remarks>
    /// On read, same integer / lenient major-minor policy as <see cref="FileVersion"/>.
    /// </remarks>
    public byte FileRevision { get; set; }

    /// <summary>
    /// Version of the EDS specification (format "x.y").
    /// EDS files according to this specification should use "4.0".
    /// </summary>
    public string EdsVersion { get; set; } = "4.0";

    /// <summary>
    /// File description (max 243 characters).
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// File creation time. EDS and DCF use "hh:mm(AM|PM)"; an XDD/XDC read keeps the
    /// <c>fileCreationTime</c> <c>xsd:time</c> text as read (for example
    /// <c>19:31:59.7179280+01:00</c>).
    /// </summary>
    /// <remarks>
    /// The XDD/XDC writers emit either form as <c>xsd:time</c> and omit a value that is neither.
    /// </remarks>
    public string CreationTime { get; set; } = string.Empty;

    /// <summary>
    /// Date of file creation (format "mm-dd-yyyy").
    /// </summary>
    /// <remarks>
    /// The XDD/XDC writers emit <c>fileCreationDate</c> (required, <c>xsd:date</c>) only for a date
    /// that exists in the calendar. Without one the attribute is omitted and the document is not
    /// schema-valid; a validated XDD/XDC write rejects the model.
    /// </remarks>
    public string CreationDate { get; set; } = string.Empty;

    /// <summary>
    /// Original <c>fileCreationDate</c> text of an XDD/XDC read (<c>xsd:date</c>, possibly with a
    /// time zone). The writer emits it while its calendar date still equals <see cref="CreationDate"/>.
    /// </summary>
    internal string? CreationDateLexical { get; set; }

    /// <summary>
    /// Name or description of the file creator (max 245 characters).
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Time of last modification. Same two forms as <see cref="CreationTime"/>.
    /// </summary>
    public string ModificationTime { get; set; } = string.Empty;

    /// <summary>
    /// Date of last file modification (format "mm-dd-yyyy").
    /// </summary>
    public string ModificationDate { get; set; } = string.Empty;

    /// <summary>
    /// Original <c>fileModificationDate</c> text of an XDD/XDC read; see <see cref="CreationDateLexical"/>.
    /// </summary>
    internal string? ModificationDateLexical { get; set; }

    /// <summary>
    /// Name or description of the modifier (max 244 characters).
    /// </summary>
    public string ModifiedBy { get; set; } = string.Empty;

    /// <summary>
    /// For DCF files: File name of the EDS file used as template.
    /// </summary>
    public string? LastEds { get; set; }

    /// <summary>
    /// Entries of the <c>[FileInfo]</c> section that the reader does not map onto a
    /// property, in file order. Keys compare case-insensitively.
    /// </summary>
    /// <remarks>
    /// CiA 306-1 allows additional entries inside the standard sections "in order to support
    /// future extensions" (§ 6.2). The EDS/DCF writers emit these entries after the keys they
    /// generate for the section. A key the writer already generates for this section is not
    /// written a second time. In a DCF, <c>LastEDS</c> is mapped onto <see cref="LastEds"/>; an EDS keeps it here.
    /// </remarks>
    public OrderedStringDictionary RemainingEntries { get; } = new();
}
