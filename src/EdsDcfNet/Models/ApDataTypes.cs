namespace EdsDcfNet.Models;

// ─── Type references ──────────────────────────────────────────────────────────

/// <summary>
/// Reference to a data type used in a variable declaration, array element type,
/// or parameter specification.
/// Exactly one of <see cref="SimpleTypeName"/> or <see cref="DataTypeIdRef"/> must be set.
/// </summary>
public class ApTypeRef
{
    /// <summary>
    /// IEC 61131-3 simple type name from the g_simple group
    /// (e.g. "BOOL", "INT", "UINT", "DINT", "UDINT", "REAL", "LREAL",
    /// "BYTE", "WORD", "DWORD", "LWORD", "STRING", "WSTRING", "BITSTRING", …).
    /// <see langword="null"/> when the type is a complex type reference.
    /// </summary>
    public string? SimpleTypeName { get; set; }

    /// <summary>
    /// References the <c>uniqueID</c> of a complex data type defined in the
    /// <c>dataTypeList</c> element via a <c>dataTypeIDRef</c> element.
    /// <see langword="null"/> when the type is a simple type.
    /// </summary>
    public string? DataTypeIdRef { get; set; }
}

// ─── Array ────────────────────────────────────────────────────────────────────

/// <summary>
/// One dimension of an array type (element <c>subrange</c>).
/// </summary>
public class ApSubrange
{
    /// <summary>Lower limit of the array dimension index.</summary>
    public long LowerLimit { get; set; }

    /// <summary>Upper limit of the array dimension index.</summary>
    public long UpperLimit { get; set; }
}

/// <summary>
/// Array data type definition (element <c>array</c> in <c>dataTypeList</c>).
/// Describes a potentially multi-dimensional array with a simple or complex element type.
/// </summary>
public class ApArrayType
{
    /// <summary>Data type name of the array type.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique ID of the array type (xsd:ID).</summary>
    public string UniqueId { get; set; } = string.Empty;

    /// <summary>Optional multilingual names / descriptions.</summary>
    public ApLabelGroup LabelGroup { get; } = new();

    /// <summary>
    /// Subrange elements defining the dimensions.
    /// One entry per dimension; the first entry is the leftmost index.
    /// At least one subrange must be present.
    /// </summary>
    public List<ApSubrange> Subranges { get; } = new();

    /// <summary>Type of the array elements (simple or complex type reference).</summary>
    public ApTypeRef? ElementType { get; set; }
}

// ─── Struct ───────────────────────────────────────────────────────────────────

/// <summary>
/// Variable declaration inside a struct or a function-type interface list
/// (element <c>varDeclaration</c>).
/// </summary>
public class ApVarDeclaration
{
    /// <summary>Name of the variable or structure component.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique ID (xsd:ID) of the variable.</summary>
    public string UniqueId { get; set; } = string.Empty;

    /// <summary>
    /// First element if the variable is of anonymous ARRAY, BITSTRING, STRING or WSTRING type.
    /// </summary>
    public string? Start { get; set; }

    /// <summary>
    /// Number of elements if the variable is of anonymous ARRAY, BITSTRING, STRING or WSTRING type.
    /// </summary>
    public string? Size { get; set; }

    /// <summary>Interpretation as a signed value; <see langword="false"/> by default.</summary>
    public bool? IsSigned { get; set; }

    /// <summary>
    /// Offset added to the value to form a scaled value:
    /// EngineeringValue = (value + offset) * multiplier.
    /// </summary>
    public string? Offset { get; set; }

    /// <summary>
    /// Scaling factor: EngineeringValue = (value + offset) * multiplier.
    /// </summary>
    public string? Multiplier { get; set; }

    /// <summary>Initial (default) value of the interface variable or structure component.</summary>
    public string? InitialValue { get; set; }

    /// <summary>Optional multilingual names / descriptions.</summary>
    public ApLabelGroup LabelGroup { get; } = new();

    /// <summary>Data type of this variable (simple type or complex type reference).</summary>
    public ApTypeRef? Type { get; set; }

    /// <summary>Conditional support references (paramIDRef values).</summary>
    public List<string> ConditionalSupports { get; } = new();

    /// <summary>Optional default value sub-element.</summary>
    public ApParameterValue? DefaultValue { get; set; }

    /// <summary>Optional allowed-values sub-element.</summary>
    public ApAllowedValues? AllowedValues { get; set; }

    /// <summary>Optional engineering unit sub-element.</summary>
    public ApUnit? Unit { get; set; }
}

/// <summary>
/// Structured data type definition (element <c>struct</c> in <c>dataTypeList</c>).
/// </summary>
public class ApStructType
{
    /// <summary>Data type name of the structured data type.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique ID of the structured data type (xsd:ID).</summary>
    public string UniqueId { get; set; } = string.Empty;

    /// <summary>Optional multilingual names / descriptions.</summary>
    public ApLabelGroup LabelGroup { get; } = new();

    /// <summary>Component variables (members) of the structure.</summary>
    public List<ApVarDeclaration> VarDeclarations { get; } = new();
}

// ─── Enum ─────────────────────────────────────────────────────────────────────

/// <summary>
/// A single enumeration constant (element <c>enumValue</c>).
/// </summary>
public class ApEnumValue
{
    /// <summary>Optional numeric value for the enumeration constant.</summary>
    public string? Value { get; set; }

    /// <summary>Multilingual names for this enumeration constant.</summary>
    public ApLabelGroup LabelGroup { get; } = new();
}

/// <summary>
/// Enumerated data type definition (element <c>enum</c> in <c>dataTypeList</c>).
/// </summary>
public class ApEnumType
{
    /// <summary>Type name of the enumerated data type.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique ID of the enumerated data type (xsd:ID).</summary>
    public string UniqueId { get; set; } = string.Empty;

    /// <summary>Optional number of enumerated values.</summary>
    public string? Size { get; set; }

    /// <summary>Optional multilingual names / descriptions.</summary>
    public ApLabelGroup LabelGroup { get; } = new();

    /// <summary>Optional simple base type for the enumeration constants.</summary>
    public string? SimpleTypeName { get; set; }

    /// <summary>The enumeration constants.</summary>
    public List<ApEnumValue> EnumValues { get; } = new();
}

// ─── Derived ──────────────────────────────────────────────────────────────────

/// <summary>
/// The count element inside a <c>derived</c> type definition (element <c>count</c>).
/// Defines the number of units of the base type used to build the derived type.
/// </summary>
public class ApDerivedCount
{
    /// <summary>Unique ID of the count element (xsd:ID).</summary>
    public string UniqueId { get; set; } = string.Empty;

    /// <summary>Access mode for the count: const, read, write, readWrite, noAccess.</summary>
    public string Access { get; set; } = "read";

    /// <summary>Optional multilingual names / descriptions.</summary>
    public ApLabelGroup LabelGroup { get; } = new();

    /// <summary>Default value of the count (the number of units).</summary>
    public ApParameterValue? DefaultValue { get; set; }

    /// <summary>Optional allowed values for the count.</summary>
    public ApAllowedValues? AllowedValues { get; set; }
}

/// <summary>
/// Derived data type definition (element <c>derived</c> in <c>dataTypeList</c>).
/// Derives a new type from a given base type.
/// </summary>
public class ApDerivedType
{
    /// <summary>Data type name of the derived type.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique ID of the derived type (xsd:ID).</summary>
    public string UniqueId { get; set; } = string.Empty;

    /// <summary>Optional multilingual names / descriptions.</summary>
    public ApLabelGroup LabelGroup { get; } = new();

    /// <summary>
    /// Optional count element defining the number of base-type units.
    /// If absent the derived type is simply a renamed alias of the base type.
    /// </summary>
    public ApDerivedCount? Count { get; set; }

    /// <summary>Base type (simple type or complex type reference).</summary>
    public ApTypeRef? BaseType { get; set; }
}
