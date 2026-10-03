namespace EdsDcfNet.Models;

/// <summary>
/// Represents the [Comments] section of an EDS/DCF file.
/// Contains additional textual comments.
/// </summary>
public class Comments
{
    /// <summary>
    /// Number of comment lines (Unsigned16).
    /// </summary>
    /// <remarks>
    /// The EDS/DCF writers do not write this value as stored: they write the number of
    /// <see cref="CommentLines"/> (at least the highest line number), so the file stays consistent
    /// when lines were added or removed after reading.
    /// </remarks>
    public ushort Lines { get; set; }

    /// <summary>
    /// List of comment lines (max 249 characters each).
    /// Key is the line number (1-based), value is the comment text.
    /// </summary>
    public Dictionary<int, string> CommentLines { get; } = new();

    /// <summary>
    /// Entries of the <c>[Comments]</c> section that the reader does not map onto a
    /// property, in file order. Keys compare case-insensitively.
    /// </summary>
    /// <remarks>
    /// CiA 306-1 allows additional entries inside the standard sections "in order to support
    /// future extensions" (§ 6.2). The EDS/DCF writers emit these entries after the keys they
    /// generate for the section. A key the writer already generates for this section is not
    /// written a second time. The same type holds a module's <c>[MxComments]</c> section (§ 8.3). <c>Line&lt;n&gt;</c> entries above <see cref="Lines"/> are not comment lines and stay here; a line the writer generates from <see cref="CommentLines"/> replaces a kept entry with the same key.
    /// </remarks>
    public OrderedStringDictionary RemainingEntries { get; } = new();
}
