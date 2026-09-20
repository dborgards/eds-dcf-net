namespace EdsDcfNet.Tests.Integration;

/// <summary>
/// Shared enumeration of the real-world corpus under Fixtures/Corpus (#525).
/// Corpus files live in Fixtures/Corpus/&lt;source&gt;/&lt;file&gt; next to the source's
/// LICENSE/NOTICE file; diagnostics snapshots live next to the file as
/// &lt;file&gt;.diagnostics.json.
/// </summary>
internal static class CorpusFiles
{
    internal static readonly string CorpusRoot = Path.Combine("Fixtures", "Corpus");

    /// <summary>
    /// Yields one row per corpus file with the given extension. xUnit errors on
    /// empty MemberData; emit a sentinel row the theories turn into a documented
    /// early return so adding the first corpus file for a format requires no
    /// test-code change.
    /// </summary>
    internal static IEnumerable<object[]> Enumerate(string extension)
    {
        var found = new List<string>();
        if (Directory.Exists(CorpusRoot))
        {
            var dottedExtension = "." + extension;
            foreach (var sourceDir in Directory.EnumerateDirectories(CorpusRoot))
                found.AddRange(Directory.EnumerateFiles(sourceDir, "*.*")
                    .Where(file => string.Equals(Path.GetExtension(file), dottedExtension, StringComparison.OrdinalIgnoreCase)));
        }

        if (found.Count == 0)
            yield return new object[] { string.Empty };

        foreach (var file in found.OrderBy(f => f, StringComparer.Ordinal))
            yield return new object[] { file };
    }

    /// <summary>No corpus file for this format yet — the sentinel row.</summary>
    internal static bool IsSentinel(string filePath) => string.IsNullOrEmpty(filePath);

    /// <summary>
    /// Returns the corpus-relative key (forward slashes) for a file inside the
    /// corpus root, e.g. "python-canopen/e35.eds".
    /// </summary>
    internal static string RelativeKey(string filePath)
    {
        // Path.GetRelativePath is unavailable on net48; Uri.MakeRelativeUri works
        // on every target and already yields forward slashes.
        var rootUri = new Uri(Path.GetFullPath(CorpusRoot) + Path.DirectorySeparatorChar);
        var relative = rootUri.MakeRelativeUri(new Uri(Path.GetFullPath(filePath)));
        return Uri.UnescapeDataString(relative.ToString());
    }

    /// <summary>
    /// Resolves the corpus root in the source tree (not the copied output tree) by
    /// walking up from the test output directory, so update-mode writes land next
    /// to the checked-in fixtures.
    /// </summary>
    internal static string FindSourceCorpusRoot()
    {
        // Walk up from bin/... to the test project root. Do not stop on a
        // Fixtures directory alone: CopyToOutputDirectory places a copy under
        // AppContext.BaseDirectory, which must not be treated as the source.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Integration")))
                return Path.Combine(dir.FullName, "Fixtures", "Corpus");
            dir = dir.Parent;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..",
            "Fixtures", "Corpus"));
    }
}
