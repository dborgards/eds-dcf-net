namespace EdsDcfNet.Models;

/// <summary>
/// Represents a single node within a CiA 306-3 network topology.
/// </summary>
public class NetworkNode
{
    /// <summary>
    /// Gets or sets the node ID (1-127).
    /// </summary>
    public byte NodeId { get; set; }

    /// <summary>
    /// Gets or sets whether the node is present in the network.
    /// </summary>
    public bool Present { get; set; }

    /// <summary>
    /// Gets or sets the node name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the node reference designator.
    /// </summary>
    public string? Refd { get; set; }

    /// <summary>
    /// Gets or sets the DCF file name for this node.
    /// </summary>
    public string? DcfFileName { get; set; }
}
