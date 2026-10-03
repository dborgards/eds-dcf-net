namespace EdsDcfNet.Models;

using EdsDcfNet.Validation;

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
    /// <remarks>
    /// <para>
    /// The writers emit these sections after the standard sections of the file, in the order in
    /// which the reader found the sections and, within a section, its keys. The
    /// <see cref="Dictionary{TKey,TValue}"/> enumeration order is not guaranteed, so the model
    /// keeps the names the reader saw as separate, internal ordering information (a list of
    /// names, not a change log; the dictionaries cannot report changes). Therefore:
    /// </para>
    /// <list type="number">
    /// <item><description>Sections and keys that are present when writing and whose name the reader saw are written in reader order.</description></item>
    /// <item><description>All other sections and keys follow, in the enumeration order of the dictionary (no further guarantee).</description></item>
    /// <item><description>A removed section or key is skipped. One that is removed and added again under the same name is written at its reader position, not at the end, and cannot be told apart from an unchanged one.</description></item>
    /// </list>
    /// <para>
    /// The original position of a section between standard sections is not kept.
    /// </para>
    /// </remarks>
    public Dictionary<string, Dictionary<string, string>> AdditionalSections { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Order of <see cref="AdditionalSections"/> and their keys as read; see the remarks there.</summary>
    internal AdditionalSectionOrder AdditionalSectionOrder { get; } = new();

    /// <summary>
    /// Validates this model instance against common CANopen constraints.
    /// </summary>
    /// <returns>List of validation issues. Empty when model is valid.</returns>
    public IReadOnlyList<ValidationIssue> Validate()
    {
        return CanOpenModelValidator.Validate(this);
    }
}
