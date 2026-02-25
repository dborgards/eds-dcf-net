namespace EdsDcfNet.Models;

/// <summary>
/// Represents the CiA 311 <c>ApplicationProcess</c> element of a device profile.
/// Describes the set of services and parameters that constitute the behaviour and
/// interfaces of the device in terms of the application, independently of the
/// device technology and the communication protocol.
/// </summary>
public class ApplicationProcess
{
    /// <summary>
    /// Optional list of complex data types (arrays, structs, enums, derived types)
    /// used in variable declarations and parameter specifications
    /// (element <c>dataTypeList</c>).
    /// </summary>
    public ApDataTypeList? DataTypeList { get; set; }

    /// <summary>
    /// Optional list of function types describing the device functions
    /// (element <c>functionTypeList</c>).
    /// </summary>
    public List<ApFunctionType> FunctionTypeList { get; } = new();

    /// <summary>
    /// Optional list of function instances at the ApplicationProcess level
    /// (element <c>functionInstanceList</c>).
    /// </summary>
    public ApFunctionInstanceList? FunctionInstanceList { get; set; }

    /// <summary>
    /// Optional template list for parameter and allowed-values templates
    /// (element <c>templateList</c>).
    /// </summary>
    public ApTemplateList? TemplateList { get; set; }

    /// <summary>
    /// Mandatory parameter list when an <c>ApplicationProcess</c> element is present
    /// (element <c>parameterList</c>).
    /// </summary>
    public List<ApParameter> ParameterList { get; } = new();

    /// <summary>
    /// Optional list of parameter groups for HMI or other classification purposes
    /// (element <c>parameterGroupList</c>).
    /// </summary>
    public List<ApParameterGroup> ParameterGroupList { get; } = new();
}

/// <summary>
/// Container for complex data type definitions: arrays, structs, enums, and derived types
/// (element <c>dataTypeList</c>).
/// </summary>
public class ApDataTypeList
{
    /// <summary>Array type definitions.</summary>
    public List<ApArrayType> Arrays { get; } = new();

    /// <summary>Struct type definitions.</summary>
    public List<ApStructType> Structs { get; } = new();

    /// <summary>Enum type definitions.</summary>
    public List<ApEnumType> Enums { get; } = new();

    /// <summary>Derived type definitions.</summary>
    public List<ApDerivedType> Derived { get; } = new();

    /// <summary>
    /// Returns <see langword="true"/> when no data types of any kind are defined.
    /// </summary>
    public bool IsEmpty =>
        Arrays.Count == 0 && Structs.Count == 0 &&
        Enums.Count == 0 && Derived.Count == 0;
}
