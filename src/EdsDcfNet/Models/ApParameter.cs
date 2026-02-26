namespace EdsDcfNet.Models;

// ─── Shared value types ───────────────────────────────────────────────────────

/// <summary>
/// A parameter value element: actualValue, defaultValue, or substituteValue.
/// Carries the value itself plus optional scaling information.
/// </summary>
public class ApParameterValue
{
    /// <summary>The value as a string (required for actualValue/defaultValue/substituteValue).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Offset added to the value for engineering-unit display.
    /// When absent the offset from the parent parameter element applies.
    /// </summary>
    public string? Offset { get; set; }

    /// <summary>
    /// Multiplier applied to the value for engineering-unit display.
    /// When absent the multiplier from the parent parameter element applies.
    /// </summary>
    public string? Multiplier { get; set; }

    /// <summary>Optional multilingual names / descriptions for this value.</summary>
    public ApLabelGroup LabelGroup { get; } = new();
}

/// <summary>A continuous range of allowed values defined by min, max, and optional step.</summary>
public class ApAllowedRange
{
    /// <summary>Lower bound of the allowed range. <see langword="null"/> when absent in source XML.</summary>
    public ApParameterValue? MinValue { get; set; }

    /// <summary>Upper bound of the allowed range. <see langword="null"/> when absent in source XML.</summary>
    public ApParameterValue? MaxValue { get; set; }

    /// <summary>Optional equidistant step between min and max.</summary>
    public ApParameterValue? Step { get; set; }
}

/// <summary>
/// Defines the set of allowed values and/or ranges for a parameter or variable declaration
/// (element <c>allowedValues</c>).
/// </summary>
public class ApAllowedValues
{
    /// <summary>
    /// Optional reference to an <c>allowedValuesTemplate</c> uniqueID.
    /// Sub-elements of this instance override those of the referenced template.
    /// </summary>
    public string? TemplateIdRef { get; set; }

    /// <summary>Individual allowed values.</summary>
    public List<ApParameterValue> Values { get; } = new();

    /// <summary>Continuous allowed ranges.</summary>
    public List<ApAllowedRange> Ranges { get; } = new();
}

/// <summary>
/// Engineering unit for a parameter (element <c>unit</c>).
/// </summary>
public class ApUnit
{
    /// <summary>Multiplier for the engineering unit of an analog parameter.</summary>
    public string Multiplier { get; set; } = string.Empty;

    /// <summary>
    /// Optional URI linking to a file containing the engineering unit definition
    /// (time, temperature, pressure, flow, acceleration, current, energy …).
    /// </summary>
    public string? UnitUri { get; set; }

    /// <summary>Optional multilingual names / descriptions for the unit.</summary>
    public ApLabelGroup LabelGroup { get; } = new();
}

/// <summary>
/// An additional vendor-specific or tool-specific property of a parameter (element <c>property</c>).
/// </summary>
public class ApProperty
{
    /// <summary>Name of the property.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Value of the property.</summary>
    public string Value { get; set; } = string.Empty;
}

// ─── VariableRef ──────────────────────────────────────────────────────────────

/// <summary>
/// References a specific component of a structured or array variable
/// (element <c>memberRef</c>).
/// </summary>
public class ApMemberRef
{
    /// <summary>
    /// Unique ID of the referenced structure component (xsd:IDREF).
    /// Used when the interface variable is of structured data type.
    /// </summary>
    public string? UniqueIdRef { get; set; }

    /// <summary>
    /// Index of the referenced array element.
    /// Used when the interface variable is of array data type.
    /// </summary>
    public long? Index { get; set; }
}

/// <summary>
/// References an interface variable of a function instance (element <c>variableRef</c>).
/// Maps a parameter to one or more interface variables via instance path + variable ID.
/// </summary>
public class ApVariableRef
{
    /// <summary>
    /// Sequence position when multiple data items are packed into a single parameter object;
    /// position 1 means starting at the lowest bit. Default is 1.
    /// </summary>
    public byte Position { get; set; } = 1;

    /// <summary>
    /// Path of function instance IDs from the ApplicationProcess root to the target instance
    /// (one <c>instanceIDRef</c> element per hierarchy level).
    /// </summary>
    public List<string> InstanceIdRefs { get; } = new();

    /// <summary>Unique ID of the referenced interface variable of the function type (xsd:IDREF).</summary>
    public string VariableIdRef { get; set; } = string.Empty;

    /// <summary>Optional reference to a specific member when the variable is structured/array.</summary>
    public ApMemberRef? MemberRef { get; set; }
}

// ─── Parameter ────────────────────────────────────────────────────────────────

/// <summary>
/// A single parameter of a device profile (element <c>parameter</c> in <c>parameterList</c>).
/// Describes device parameters independently of the communication network.
/// </summary>
public class ApParameter
{
    /// <summary>Unique ID of the parameter (xsd:ID).</summary>
    public string UniqueId { get; set; } = string.Empty;

    /// <summary>
    /// Primary access mode:
    /// const, read, write, readWrite, readWriteInput, readWriteOutput, noAccess.
    /// Default is "read".
    /// </summary>
    public string Access { get; set; } = "read";

    /// <summary>
    /// Additional allowed access modes (space-separated list of NMTOKENS).
    /// Same values as <see cref="Access"/>; the parser will not enforce them.
    /// </summary>
    public string? AccessList { get; set; }

    /// <summary>
    /// Whether the parameter must be implemented:
    /// mandatory, optional, or conditional.
    /// </summary>
    public string? Support { get; set; }

    /// <summary>
    /// If <see langword="true"/> the value persists after a power failure.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool Persistent { get; set; }

    /// <summary>
    /// Offset for engineering-unit display:
    /// EngineeringValue = (ParameterValue + offset) * multiplier.
    /// </summary>
    public string? Offset { get; set; }

    /// <summary>
    /// Multiplier for engineering-unit display:
    /// EngineeringValue = (ParameterValue + offset) * multiplier.
    /// </summary>
    public string? Multiplier { get; set; }

    /// <summary>Optional reference to a <c>parameterTemplate</c> uniqueID.</summary>
    public string? TemplateIdRef { get; set; }

    /// <summary>Multilingual names / descriptions (g_labels group).</summary>
    public ApLabelGroup LabelGroup { get; } = new();

    /// <summary>
    /// Data type reference (simple type from g_simple or complex type via dataTypeIDRef).
    /// Mutually exclusive with <see cref="VariableRefs"/>.
    /// </summary>
    public ApTypeRef? TypeRef { get; set; }

    /// <summary>
    /// References to interface variables of function instances.
    /// Mutually exclusive with <see cref="TypeRef"/> (per NOTE 1 in the spec).
    /// </summary>
    public List<ApVariableRef> VariableRefs { get; } = new();

    /// <summary>
    /// Conditional support dependencies.
    /// Each entry is the uniqueID of an optional parameter that must be present
    /// for this conditional parameter to be required.
    /// </summary>
    public List<string> ConditionalSupports { get; } = new();

    /// <summary>
    /// Application-specific multilingual parameter names (element <c>denotation</c>).
    /// </summary>
    public ApLabelGroup? Denotation { get; set; }

    /// <summary>Current actual value of the parameter (element <c>actualValue</c>).</summary>
    public ApParameterValue? ActualValue { get; set; }

    /// <summary>Default value of the parameter (element <c>defaultValue</c>).</summary>
    public ApParameterValue? DefaultValue { get; set; }

    /// <summary>
    /// Substitute value provided to the device in certain operating states, e.g. on fault
    /// (element <c>substituteValue</c>).
    /// </summary>
    public ApParameterValue? SubstituteValue { get; set; }

    /// <summary>Allowed values and/or ranges (element <c>allowedValues</c>).</summary>
    public ApAllowedValues? AllowedValues { get; set; }

    /// <summary>Engineering unit of the parameter (element <c>unit</c>).</summary>
    public ApUnit? Unit { get; set; }

    /// <summary>Vendor-specific additional properties (element <c>property</c>).</summary>
    public List<ApProperty> Properties { get; } = new();
}
