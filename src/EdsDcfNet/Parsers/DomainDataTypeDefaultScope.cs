namespace EdsDcfNet.Parsers;

/// <summary>
/// Ambient opt-in for the CiA 306-1 Table 7 DOMAIN sub-index <c>DataType</c> default.
/// Used so <see cref="CanOpenFileOptions.ApplyDomainDataTypeDefault"/> can reach the
/// EDS/DCF sub-index parsers without changing reader constructor shapes.
/// </summary>
internal static class DomainDataTypeDefaultScope
{
    private static readonly AsyncLocal<bool> Enabled = new();

    public static bool IsEnabled => Enabled.Value;

    public static IDisposable Enter(bool applyDefault)
    {
        var previous = Enabled.Value;
        Enabled.Value = applyDefault;
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
