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
    public ushort Lines { get; set; }

    /// <summary>
    /// List of comment lines (max 249 characters each).
    /// Key is the line number (1-based), value is the comment text.
    /// </summary>
    public Dictionary<int, string> CommentLines { get; set; } = new();
}
