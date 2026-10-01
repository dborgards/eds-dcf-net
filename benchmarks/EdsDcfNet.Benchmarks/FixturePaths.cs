namespace EdsDcfNet.Benchmarks;

internal static class FixturePaths
{
    public static string Get(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
    }
}
