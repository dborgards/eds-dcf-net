namespace EdsDcfNet.Diagnostics;

/// <summary>
/// Result of a <c>Read*WithDiagnostics</c> call: the parsed model plus every lenient-mode
/// repair the parser applied along the way.
/// </summary>
/// <typeparam name="TModel">The in-memory model type for the format.</typeparam>
public sealed class CanOpenReadResult<TModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CanOpenReadResult{TModel}"/> class.
    /// </summary>
    /// <param name="model">The parsed model.</param>
    /// <param name="diagnostics">The lenient-mode repairs reported while parsing.</param>
    public CanOpenReadResult(TModel model, IReadOnlyList<ParseDiagnostic> diagnostics)
    {
        Model = model;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the parsed model.</summary>
    public TModel Model { get; }

    /// <summary>Gets the lenient-mode repairs reported while parsing (empty when the input was clean).</summary>
    public IReadOnlyList<ParseDiagnostic> Diagnostics { get; }

    /// <summary>Gets whether any diagnostics were reported.</summary>
    public bool HasDiagnostics => Diagnostics.Count > 0;
}
