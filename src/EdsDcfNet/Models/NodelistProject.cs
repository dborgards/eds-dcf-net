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
    /// The writers keep the reader order of these sections and their keys. A read section is
    /// inserted before the next generated section from the source file; read sections at the end
    /// and newly added sections follow the generated content. The
    /// <see cref="Dictionary{TKey,TValue}"/> enumeration order is not guaranteed, so the model
    /// keeps separate internal ordering and insertion information (not a change log; the
    /// dictionaries cannot report changes). Therefore:
    /// </para>
    /// <list type="number">
    /// <item><description>Sections and keys that are present when writing and whose name the reader saw are written in reader order.</description></item>
    /// <item><description>New sections and keys follow in dictionary enumeration order; a new section follows the generated content.</description></item>
    /// <item><description>A removed section or key is skipped. One that is removed and added again under the same name takes its reader position again and cannot be told apart from an unchanged one.</description></item>
    /// </list>
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
