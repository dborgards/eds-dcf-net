namespace EdsDcfNet.Checker;

/// <summary>Path helpers shared by the config globs and the SARIF writer.</summary>
internal static class Paths
{
    /// <summary>
    /// The path of <paramref name="file"/> relative to <paramref name="baseDirectory"/>, or
    /// <see langword="null"/> when the file is not below it (another drive or a <c>..</c> step).
    /// </summary>
    public static string? RelativeTo(string baseDirectory, string file)
    {
        var full = Path.GetFullPath(file);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory)) + Path.DirectorySeparatorChar;
        return new Uri(root).IsBaseOf(new Uri(full)) ? Path.GetRelativePath(root, full) : null;
    }
}
