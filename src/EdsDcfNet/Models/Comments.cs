namespace EdsDcfNet.Models;

using System.Globalization;

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
    /// when lines were added or removed after reading. Keep the line numbers contiguous from 1: a
    /// validated EDS/DCF write rejects a gap, because the file would lack a <c>Line&lt;n&gt;</c>
    /// inside <c>Lines</c>.
    /// </remarks>
    public ushort Lines { get; set; }

    /// <summary>
    /// Finds the first line number in <c>1..WrittenLineCount()</c> that the writer would not emit: neither a
    /// <see cref="CommentLines"/> entry nor a preserved <c>Line&lt;n&gt;</c> in <see cref="RemainingEntries"/>.
    /// </summary>
    internal bool TryFindMissingLine(out int number)
    {
        var written = WrittenLineCount();
        for (var n = 1; n <= written; n++)
        {
            if (CommentLines.ContainsKey(n)
                || RemainingEntries.ContainsKey(string.Format(CultureInfo.InvariantCulture, "Line{0}", n)))
            {
                continue;
            }

            number = n;
            return true;
        }

        number = 0;
        return false;
    }

    /// <summary>
    /// The <c>Lines</c> value the EDS/DCF writers emit for these comments: the number of
    /// <see cref="CommentLines"/>, raised to the highest line number so a gap never hides a line, and
    /// to an empty <c>Line&lt;n&gt;</c> the reader kept inside the file's own count
    /// (<see cref="RemainingEntries"/>), so such a file keeps its <c>Lines</c>.
    /// </summary>
    internal ushort WrittenLineCount()
    {
        // Line numbers start at 1; a key below 1 is not a comment line.
        var count = 0;
        foreach (var number in CommentLines.Keys)
        {
            if (number >= 1)
                count++;
        }

        foreach (var number in CommentLines.Keys)
        {
            if (number > count)
                count = number;
        }

        foreach (var entry in RemainingEntries)
        {
            if (entry.Key.StartsWith("Line", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(entry.Key[4..], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                && number <= Lines
                && number > count)
            {
                count = number;
            }
        }

        return (ushort)Math.Min(count, ushort.MaxValue);
    }

    /// <summary>
    /// List of comment lines (max 249 characters each).
    /// Key is the line number (1-based, contiguous for a validated EDS/DCF write), value is the comment text.
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
