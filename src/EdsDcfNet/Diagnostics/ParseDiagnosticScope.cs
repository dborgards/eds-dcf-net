namespace EdsDcfNet.Diagnostics;

/// <summary>
/// Ambient diagnostic sink for the duration of a <c>Read*WithDiagnostics</c> facade call.
/// Mirrors <see cref="Parsers.StrictParsingScope"/>: an <see cref="AsyncLocal{T}"/> keeps the
/// collector scoped to the current async flow, so concurrent reads with and without
/// diagnostics do not interfere, and readers/parsers can report without signature changes.
/// </summary>
internal static class ParseDiagnosticScope
{
    private static readonly AsyncLocal<List<ParseDiagnostic>?> Current = new();

    /// <summary>Gets whether a diagnostics collector is active in the current flow.</summary>
    internal static bool IsCollecting => Current.Value != null;

    /// <summary>
    /// Starts collecting diagnostics in the current async flow. Dispose the returned scope to
    /// stop collecting and restore any outer collector.
    /// </summary>
    internal static CollectingScope Enter() => new();

    /// <summary>
    /// Reports a lenient-mode repair. No-op when no collector is active (direct reader APIs).
    /// </summary>
    internal static void Report(ParseDiagnostic diagnostic)
    {
        Current.Value?.Add(diagnostic);
    }

    internal sealed class CollectingScope : IDisposable
    {
        private readonly List<ParseDiagnostic>? _previous;
        private readonly List<ParseDiagnostic> _collected = new();
        private bool _disposed;

        internal CollectingScope()
        {
            _previous = Current.Value;
            Current.Value = _collected;
        }

        /// <summary>Gets the diagnostics collected so far in this scope.</summary>
        internal IReadOnlyList<ParseDiagnostic> Diagnostics => _collected.ToArray();

        public void Dispose()
        {
            if (_disposed)
                return;

            Current.Value = _previous;
            _disposed = true;
        }
    }
}
