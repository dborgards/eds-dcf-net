namespace EdsDcfNet.Parsers;

/// <summary>
/// Case-insensitive INI section that records key insertion order.
/// </summary>
/// <remarks>
/// <see cref="Dictionary{TKey,TValue}"/> enumeration is not insertion order on .NET Framework.
/// Object and sub-object sections copy unknown keys from this order list so round-trips match the file.
/// Callers must insert through <see cref="Set"/>; the inherited indexer does not update the order list.
/// </remarks>
internal sealed class IniSectionDictionary : Dictionary<string, string>
{
    private readonly List<string> _order = new();

    internal IniSectionDictionary()
        : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    internal void Set(string key, string value)
    {
        if (ContainsKey(key))
        {
            this[key] = value;
            return;
        }

        Add(key, value);
        _order.Add(key);
    }

    internal IEnumerable<KeyValuePair<string, string>> EntriesInOrder()
    {
        foreach (var key in _order)
            yield return new KeyValuePair<string, string>(key, this[key]);
    }
}
