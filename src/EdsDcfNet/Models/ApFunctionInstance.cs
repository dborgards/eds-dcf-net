namespace EdsDcfNet.Models;

/// <summary>
/// A concrete instance of a function type (element <c>functionInstance</c>).
/// </summary>
public class ApFunctionInstance
{
    /// <summary>Name of the function instance.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique ID of the function instance (xsd:ID).</summary>
    public string UniqueId { get; set; } = string.Empty;

    /// <summary>Reference to the unique ID of the instantiated function type (xsd:IDREF).</summary>
    public string TypeIdRef { get; set; } = string.Empty;

    /// <summary>Optional multilingual names / descriptions.</summary>
    public ApLabelGroup LabelGroup { get; } = new();
}

/// <summary>
/// A signal connection between the output variable of one function instance and
/// the input variable of another (element <c>connection</c>).
/// </summary>
public class ApConnection
{
    /// <summary>
    /// Starting point of the connection.
    /// Typically encoded as &lt;instance_name&gt;.&lt;variable_name&gt;.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Endpoint of the connection (same encoding as <see cref="Source"/>).</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>Optional textual description of the connection.</summary>
    public string? Description { get; set; }
}

/// <summary>
/// A list of function instances and their optional interconnections
/// (element <c>functionInstanceList</c>).
/// Appears both at the <c>ApplicationProcess</c> level and nested inside a
/// <c>functionType</c> for hierarchically structured functions.
/// </summary>
public class ApFunctionInstanceList
{
    /// <summary>Function instances in this list.</summary>
    public List<ApFunctionInstance> FunctionInstances { get; } = new();

    /// <summary>Connections between function instances.</summary>
    public List<ApConnection> Connections { get; } = new();
}
