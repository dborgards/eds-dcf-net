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
}
