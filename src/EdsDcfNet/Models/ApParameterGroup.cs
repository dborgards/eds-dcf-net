namespace EdsDcfNet.Models;

/// <summary>
/// A group of parameters serving a specific purpose, e.g. an HMI view
/// (element <c>parameterGroup</c>).
/// Groups may be nested to form a hierarchy.
/// </summary>
public class ApParameterGroup
{
    /// <summary>Unique ID of the parameter group (xsd:ID).</summary>
    public string UniqueId { get; set; } = string.Empty;

    /// <summary>
    /// Classifies the parameters in this group (vendor-defined string),
    /// e.g. indicating the intended HMI view type.
    /// </summary>
    public string? KindOfAccess { get; set; }

    /// <summary>Multilingual names / descriptions.</summary>
    public ApLabelGroup LabelGroup { get; } = new();

    /// <summary>Nested sub-groups (optional, forms a hierarchy).</summary>
    public List<ApParameterGroup> SubGroups { get; } = new();

    /// <summary>
    /// References to parameters in the <c>parameterList</c> of the enclosing
    /// <c>ApplicationProcess</c> element (uniqueIDRef values).
    /// </summary>
    public List<string> ParameterRefs { get; } = new();
}
