namespace EdsDcfNet.Models;

/// <summary>
/// Template for parameter sub-element definitions (element <c>parameterTemplate</c>).
/// Shares the same attribute set as <see cref="ApParameter"/> and a subset of its sub-elements.
/// </summary>
public class ApParameterTemplate
{
    /// <summary>Unique ID of the parameter template (xsd:ID).</summary>
    public string UniqueId { get; set; } = string.Empty;

    /// <summary>
    /// Primary access mode (const, read, write, readWrite, readWriteInput,
    /// readWriteOutput, noAccess). Default is "read".
    /// </summary>
    public string Access { get; set; } = "read";

    /// <summary>Additional allowed access modes (space-separated NMTOKENS).</summary>
    public string? AccessList { get; set; }

    /// <summary>Implementation requirement: mandatory, optional, or conditional.</summary>
    public string? Support { get; set; }

    /// <summary>If <see langword="true"/> the value persists after power failure.</summary>
    public bool Persistent { get; set; }

    /// <summary>Engineering-unit offset.</summary>
    public string? Offset { get; set; }

    /// <summary>Engineering-unit multiplier.</summary>
    public string? Multiplier { get; set; }

    /// <summary>Multilingual names / descriptions.</summary>
    public ApLabelGroup LabelGroup { get; } = new();

    /// <summary>Type reference (simple or complex type).</summary>
    public ApTypeRef? TypeRef { get; set; }

    /// <summary>Conditional support dependencies (paramIDRef values).</summary>
    public List<string> ConditionalSupports { get; } = new();

    /// <summary>Actual value.</summary>
    public ApParameterValue? ActualValue { get; set; }

    /// <summary>Default value.</summary>
    public ApParameterValue? DefaultValue { get; set; }

    /// <summary>Substitute value.</summary>
    public ApParameterValue? SubstituteValue { get; set; }

    /// <summary>Allowed values / ranges.</summary>
    public ApAllowedValues? AllowedValues { get; set; }

    /// <summary>Engineering unit.</summary>
    public ApUnit? Unit { get; set; }

    /// <summary>Additional properties.</summary>
    public List<ApProperty> Properties { get; } = new();
}

/// <summary>
/// Template for allowed-values definitions (element <c>allowedValuesTemplate</c>).
/// Referenced from parameter or allowedValues elements via templateIDRef.
/// </summary>
public class ApAllowedValuesTemplate
{
    /// <summary>Unique ID of the allowed-values template (xsd:ID).</summary>
    public string UniqueId { get; set; } = string.Empty;

    /// <summary>Individual allowed values.</summary>
    public List<ApParameterValue> Values { get; } = new();

    /// <summary>Continuous allowed ranges.</summary>
    public List<ApAllowedRange> Ranges { get; } = new();
}

/// <summary>
/// Template list (element <c>templateList</c>).
/// Contains <c>parameterTemplate</c> and/or <c>allowedValuesTemplate</c> elements
/// to reduce XML file size through shared definitions.
/// </summary>
public class ApTemplateList
{
    /// <summary>Parameter templates keyed by uniqueID for O(1) lookup.</summary>
    public List<ApParameterTemplate> ParameterTemplates { get; } = new();

    /// <summary>Allowed-values templates keyed by uniqueID for O(1) lookup.</summary>
    public List<ApAllowedValuesTemplate> AllowedValuesTemplates { get; } = new();
}
