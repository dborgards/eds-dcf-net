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
    /// File creation time (format "hh:mm(AM|PM)").
    /// </summary>
    public string CreationTime { get; set; } = string.Empty;

    /// <summary>
    /// Date of file creation (format "mm-dd-yyyy").
    /// </summary>
    public string CreationDate { get; set; } = string.Empty;

    /// <summary>
    /// Name or description of the file creator (max 245 characters).
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Time of last modification (format "hh:mm(AM|PM)").
    /// </summary>
    public string ModificationTime { get; set; } = string.Empty;

    /// <summary>
    /// Date of last file modification (format "mm-dd-yyyy").
    /// </summary>
    public string ModificationDate { get; set; } = string.Empty;

    /// <summary>
    /// Name or description of the modifier (max 244 characters).
    /// </summary>
    public string ModifiedBy { get; set; } = string.Empty;

    /// <summary>
    /// For DCF files: File name of the EDS file used as template.
    /// </summary>
    public string? LastEds { get; set; }
}
