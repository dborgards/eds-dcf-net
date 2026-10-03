namespace EdsDcfNet.Parsers;

/// <summary>
/// Case-insensitive collection of the INI sections of a file that records the order of the
/// section headers. <see cref="Dictionary{TKey,TValue}"/> enumeration order is not a
/// guaranteed insertion order, so consumers that must keep the file order read
/// <see cref="NamesInOrder"/>. The parser inserts each new section through <see cref="Set"/> (the inherited indexer does not record the order).
/// </summary>
internal sealed class IniSectionsDictionary : Dictionary<string, Dictionary<string, string>>
{
    private readonly List<string> _order = new();

    internal IniSectionsDictionary()
        : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    internal IReadOnlyList<string> NamesInOrder => _order;

    internal void Set(string name, Dictionary<string, string> section)
    {
        Add(name, section);
        _order.Add(name);
    }
}
