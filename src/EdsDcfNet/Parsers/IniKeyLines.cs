namespace EdsDcfNet.Parsers;

using System.Runtime.CompilerServices;

/// <summary>
/// Source line numbers for INI keys, keyed by the sections dictionary produced by
/// <see cref="IniParser"/>. Readers use this to attribute lenient numeric coercions
/// without changing the public <c>Dictionary&lt;string, Dictionary&lt;string, string&gt;&gt;</c>
/// parse result.
/// </summary>
internal static class IniKeyLines
{
    private static readonly ConditionalWeakTable<Dictionary<string, Dictionary<string, string>>, LineMap> Lines = new();

    /// <summary>
    /// Records the source line of <paramref name="key"/> in <paramref name="sectionName"/>.
    /// A later duplicate overwrites the line (last write wins, matching the stored value).
    /// </summary>
    internal static void Record(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string key,
        int lineNumber)
    {
        var map = Lines.GetOrCreateValue(sections);
        if (!map.BySection.TryGetValue(sectionName, out var byKey))
        {
            byKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            map.BySection[sectionName] = byKey;
        }

        byKey[key] = lineNumber;
    }

    /// <summary>
    /// Returns the source line recorded for <paramref name="key"/>, or <see langword="null"/>
    /// when this sections dictionary was not produced by <see cref="IniParser"/>.
    /// </summary>
    internal static int? TryGetLine(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string key)
    {
        if (!Lines.TryGetValue(sections, out var map))
            return null;

        if (!map.BySection.TryGetValue(sectionName, out var byKey))
            return null;

        return byKey.TryGetValue(key, out var line) ? line : null;
    }

    private sealed class LineMap
    {
        internal Dictionary<string, Dictionary<string, int>> BySection { get; } =
            new(StringComparer.OrdinalIgnoreCase);
    }
}
