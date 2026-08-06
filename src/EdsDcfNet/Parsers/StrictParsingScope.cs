namespace EdsDcfNet.Parsers;

/// <summary>
/// Ambient opt-in strict-parse flag for the duration of a facade read call.
/// Used so <see cref="CanOpenFileOptions.StrictParsing"/> can reach
/// <see cref="IniParser"/> and XDD primitives without changing
/// <see cref="FormatCanOpenOperations{TModel}"/> constructor delegate shapes.
/// </summary>
internal static class StrictParsingScope
{
    private static readonly AsyncLocal<bool> Enabled = new();

    public static bool IsEnabled => Enabled.Value;

    public static IDisposable Enter(bool strictParsing)
    {
        var previous = Enabled.Value;
        Enabled.Value = strictParsing;
        return new Restorer(previous);
    }

    private sealed class Restorer : IDisposable
    {
        private readonly bool _previous;
        private bool _disposed;

        public Restorer(bool previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
                return;

            Enabled.Value = _previous;
            _disposed = true;
        }
    }
}
