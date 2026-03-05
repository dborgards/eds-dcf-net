namespace EdsDcfNet.Utilities;

internal static class AdditionalSectionsCloner
{
    internal static Dictionary<string, string> CloneSectionEntriesCaseInsensitive(Dictionary<string, string> source)
    {
        var clone = new Dictionary<string, string>(source.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in source)
        {
            clone[entry.Key] = entry.Value;
        }

        return clone;
    }
}
