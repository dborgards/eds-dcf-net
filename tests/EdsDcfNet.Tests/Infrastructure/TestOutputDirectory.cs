namespace EdsDcfNet.Tests.Infrastructure;

/// <summary>
/// Directory of this test assembly, where fixtures and baselines are copied.
/// </summary>
/// <remarks>
/// net48 runs with xUnit AppDomains denied so coverlet can load the instrumented
/// strong-named library. <see cref="AppContext.BaseDirectory"/> is then the vstest
/// testhost folder. The assembly location stays this output directory on every TFM.
/// </remarks>
internal static class TestOutputDirectory
{
    internal static string Value { get; } = Resolve();

    private static string Resolve()
    {
        var location = typeof(TestOutputDirectory).Assembly.Location;
        var directory = Path.GetDirectoryName(location);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("The test assembly location is not a file path.");
        }

        return directory;
    }
}
