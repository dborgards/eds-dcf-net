namespace EdsDcfNet.Models;

/// <summary>
/// Represents a tool entry from the [ToolX] section.
/// </summary>
public class ToolInfo
{
    /// <summary>
    /// Symbolic name of the tool.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Command line with optional placeholders ($DCF, $EDS, $NODEID, etc.).
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Entries of the <c>[ToolX]</c> section that the reader does not map onto a
    /// property, in file order. Keys compare case-insensitively.
    /// </summary>
    /// <remarks>
    /// CiA 306-1 allows additional entries inside the standard sections "in order to support
    /// future extensions" (§ 6.2). The EDS/DCF writers emit these entries after the keys they
    /// generate for the section. A key the writer already generates for this section is not
    /// written a second time.
    /// </remarks>
    public OrderedStringDictionary RemainingEntries { get; } = new();
}
