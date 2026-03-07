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
    /// Section names are compared case-insensitively using <see cref="StringComparer.OrdinalIgnoreCase"/>,
    /// so assigning names that differ only by case overwrites the previous section.
    /// Each section contains key-value pairs that are treated case-insensitively by readers/writers.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> AdditionalSections { get; } =
        new(StringComparer.OrdinalIgnoreCase);
}
