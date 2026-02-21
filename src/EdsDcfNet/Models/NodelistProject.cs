namespace EdsDcfNet.Models;

/// <summary>
/// Represents a CiA 306-3 nodelist project (.cpj) file containing one or more network topologies.
/// </summary>
public class NodelistProject
{
    /// <summary>
    /// Gets or sets the list of network topologies defined in this project.
    /// </summary>
    public List<NetworkTopology> Networks { get; } = new();

    /// <summary>
    /// Gets or sets additional sections not recognized as topology sections.
    /// Key is the section name, value is a dictionary of key-value pairs.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> AdditionalSections { get; } = new();
}
