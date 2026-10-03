namespace EdsDcfNet.Tests.Infrastructure;

/// <summary>
/// Directory of this test assembly, where fixtures and baselines are copied.
/// </summary>
/// <remarks>
/// Fixtures and baselines are copied next to this assembly. Its location is that
/// output directory on every target framework, including when a runner's
/// <see cref="AppContext.BaseDirectory"/> is a different host folder.
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
