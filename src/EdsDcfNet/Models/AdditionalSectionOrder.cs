namespace EdsDcfNet.Models;

using EdsDcfNet.Parsers;

/// <summary>
/// The order in which a reader saw the additional sections of a file and the keys of each
/// section. <c>AdditionalSections</c> is a public <see cref="Dictionary{TKey,TValue}"/> whose
/// enumeration order is not guaranteed (notably after removing and re-adding entries), so the
/// writers consult this list of names instead.
/// </summary>
/// <remarks>
/// This is a list of names, not a change log: the public dictionaries have no change hook.
/// Names the reader saw come first, in reader order, as far as they are still present.
/// Everything else follows in dictionary enumeration order. An entry that was removed and
/// re-added under the same name takes its reader position again.
/// </remarks>
internal sealed class AdditionalSectionOrder
{
    private readonly List<string> _sections = new();
    private readonly Dictionary<string, List<string>> _keys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Records the additional sections the reader placed in <paramref name="additionalSections"/>,
    /// in the order of <paramref name="fileSections"/> (the file order), with the keys of each.
    /// </summary>
    internal void Capture(
        Dictionary<string, Dictionary<string, string>> additionalSections,
        Dictionary<string, Dictionary<string, string>> fileSections)
    {
        _sections.Clear();
        _keys.Clear();

        foreach (var pair in fileSections)
        {
            if (!additionalSections.ContainsKey(pair.Key))
                continue;

            _sections.Add(pair.Key);
            _keys[pair.Key] = KeysInFileOrder(pair.Value);
        }
    }

    /// <summary>Replaces this order with a copy of <paramref name="source"/>.</summary>
    internal void CopyFrom(AdditionalSectionOrder source)
    {
        _sections.Clear();
        _keys.Clear();
        _sections.AddRange(source._sections);
        foreach (var pair in source._keys)
            _keys[pair.Key] = new List<string>(pair.Value);
    }

    /// <summary>The sections of <paramref name="additionalSections"/> in write order.</summary>
    internal IEnumerable<KeyValuePair<string, Dictionary<string, string>>> Sections(
        Dictionary<string, Dictionary<string, string>> additionalSections)
        => InOrder(additionalSections, _sections);

    /// <summary>The entries of section <paramref name="sectionName"/> in write order.</summary>
    internal IEnumerable<KeyValuePair<string, string>> Entries(
        string sectionName,
        Dictionary<string, string> entries)
        => InOrder(entries, _keys.TryGetValue(sectionName, out var keys) ? keys : null);

    private static List<string> KeysInFileOrder(Dictionary<string, string> section)
    {
        var keys = new List<string>(section.Count);
        if (section is IniSectionDictionary ordered)
        {
            foreach (var entry in ordered.EntriesInOrder())
                keys.Add(entry.Key);
        }
        else
        {
            keys.AddRange(section.Keys);
        }

        return keys;
    }

    private static IEnumerable<KeyValuePair<string, TValue>> InOrder<TValue>(
        Dictionary<string, TValue> source,
        List<string>? recorded)
    {
        if (recorded == null)
        {
            foreach (var pair in source)
                yield return pair;
            yield break;
        }

        // Index by the dictionary's own comparer so that the spelling written is the one now
        // in the dictionary, and a name matched by case-insensitive comparison counts once.
        var pending = new Dictionary<string, KeyValuePair<string, TValue>>(source.Count, source.Comparer);
        foreach (var pair in source)
            pending[pair.Key] = pair;

        foreach (var name in recorded)
        {
            if (pending.TryGetValue(name, out var pair))
            {
                pending.Remove(name);
                yield return pair;
            }
        }

        foreach (var pair in source)
        {
            if (pending.ContainsKey(pair.Key))
                yield return pair;
        }
    }
}
