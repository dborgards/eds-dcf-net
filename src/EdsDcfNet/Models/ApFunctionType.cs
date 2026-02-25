namespace EdsDcfNet.Models;

/// <summary>
/// Versioning history entry for a function type (element <c>versionInfo</c>).
/// </summary>
public class ApVersionInfo
{
    /// <summary>Name of the organisation maintaining the function type.</summary>
    public string Organization { get; set; } = string.Empty;

    /// <summary>Version identifier in the versioning history (suggested format: "xx.yy").</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Name of the person maintaining the function type.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Date of this version entry (xsd:date, stored as ISO 8601 string "YYYY-MM-DD").</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>Optional multilingual names / descriptions for this version entry.</summary>
    public ApLabelGroup LabelGroup { get; } = new();
}

/// <summary>
/// The interface definition of a function type (element <c>interfaceList</c>).
/// Contains input, output, and configuration variables.
/// </summary>
public class ApInterfaceList
{
    /// <summary>Input variables of the function type (element <c>inputVars</c>).</summary>
    public List<ApVarDeclaration> InputVars { get; } = new();

    /// <summary>Output variables of the function type (element <c>outputVars</c>).</summary>
    public List<ApVarDeclaration> OutputVars { get; } = new();

    /// <summary>Configuration variables of the function type (element <c>configVars</c>).</summary>
    public List<ApVarDeclaration> ConfigVars { get; } = new();
}

/// <summary>
/// Type description of a device function (element <c>functionType</c>).
/// Referenced from one or more function instances.
/// </summary>
public class ApFunctionType
{
    /// <summary>Type name of the function type.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique ID of the function type (xsd:ID).</summary>
    public string UniqueId { get; set; } = string.Empty;

    /// <summary>
    /// Optional textual association with a "package" or similar classification scheme.
    /// </summary>
    public string? Package { get; set; }

    /// <summary>Multilingual names / descriptions (g_labels group).</summary>
    public ApLabelGroup LabelGroup { get; } = new();

    /// <summary>
    /// Versioning history; the first entry is the most recent version,
    /// the last entry is the first released version.
    /// At least one entry must be present.
    /// </summary>
    public List<ApVersionInfo> VersionInfos { get; } = new();

    /// <summary>
    /// Interface of the function type (input, output, and configuration variables).
    /// </summary>
    public ApInterfaceList? InterfaceList { get; set; }

    /// <summary>
    /// Optional nested function instances (present only for hierarchically structured functions).
    /// </summary>
    public ApFunctionInstanceList? FunctionInstanceList { get; set; }
}
