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
    public Dictionary<byte, NetworkNode> Nodes { get; } = new();

    /// <summary>
    /// Gets the entries of this topology section that the reader does not map onto a property, in file
    /// order: manufacturer-specific keys and the node entries (<c>NodeXName</c>, <c>NodeXRefd</c>,
    /// <c>NodeXDCFName</c>, or a <c>NodeXPresent</c> without a usable value) of a node X that has no
    /// loaded <c>NodeXPresent</c> and therefore is not in <see cref="Nodes"/>
    /// (CiA 306-3 v1.2.0, section 6.2.2, Table 3).
    /// </summary>
    /// <remarks>
    /// <see cref="Writers.CpjWriter"/> writes these entries after the generated keys, in this order. A
    /// key that the writer generates itself wins: <c>NetName</c>, <c>NetRefd</c>, <c>Nodes</c>,
    /// <c>EDSBaseName</c> and every key of a node that is in <see cref="Nodes"/> are not written from
    /// here. Each topology section ([Topology], [Topology2], ...) keeps its own entries.
    /// </remarks>
    public OrderedStringDictionary RemainingEntries { get; } = new();
}
