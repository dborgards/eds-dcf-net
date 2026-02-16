namespace EdsDcfNet.Models;

/// <summary>
/// Represents a single network topology within a CiA 306-3 nodelist project.
/// </summary>
public class NetworkTopology
{
    /// <summary>
    /// Gets or sets the network name.
    /// </summary>
    public string? NetName { get; set; }

    /// <summary>
    /// Gets or sets the network reference designator.
    /// </summary>
    public string? NetRefd { get; set; }

    /// <summary>
    /// Gets or sets the base path for EDS/DCF files referenced by nodes.
    /// </summary>
    public string? EdsBaseName { get; set; }

    /// <summary>
    /// Gets or sets the nodes in this network, keyed by their node ID (1-127).
    /// </summary>
    public Dictionary<byte, NetworkNode> Nodes { get; set; } = new();
}
