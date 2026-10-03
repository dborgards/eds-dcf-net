namespace EdsDcfNet.Utilities;

/// <summary>
/// Pure path step of the symbolic-link walk in <see cref="PlatformFileSupport.ResolveLinkTarget"/>.
/// <c>FileSystemInfo.ResolveLinkTarget</c> turns a UNC target (<c>\\server\share\x</c>) into a
/// malformed local path on Windows, so the writer follows the raw link text itself.
/// </summary>
internal static class SymbolicLinkPath
{
    /// <summary>Maximum number of links followed before the walk gives up (loop protection).</summary>
    internal const int MaxDepth = 32;

    /// <summary>
    /// The path a link at <paramref name="linkPath"/> with the stored text
    /// <paramref name="rawTarget"/> points to. A rooted target (absolute path, drive path, UNC
    /// <c>\\server\share</c>, device path <c>\\?\UNC\…</c>) is used as is; a relative target is
    /// combined with the link's directory. The result is not normalized, so <c>..</c> is resolved
    /// by the file system and a UNC prefix is never rewritten.
    /// </summary>
    internal static string Next(string linkPath, string rawTarget)
    {
        if (Path.IsPathRooted(rawTarget))
            return rawTarget;

        var directory = Path.GetDirectoryName(linkPath);
        return string.IsNullOrEmpty(directory) ? rawTarget : Path.Combine(directory, rawTarget);
    }
}
