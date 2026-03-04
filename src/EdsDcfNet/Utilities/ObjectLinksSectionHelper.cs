namespace EdsDcfNet.Utilities;

using System.Globalization;
using EdsDcfNet.Models;

internal static class ObjectLinksSectionHelper
{
    public static bool IsObjectLinksSectionForExistingObject(string sectionName, ObjectDictionary objectDictionary)
    {
        if (!TryParseObjectLinksSectionIndex(sectionName, out var index))
            return false;

        return objectDictionary.Objects.ContainsKey(index);
    }

    private static bool TryParseObjectLinksSectionIndex(string sectionName, out ushort index)
    {
        const string suffix = "ObjectLinks";
        index = 0;

        if (!sectionName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return false;

        var indexPart = sectionName[..^suffix.Length];
        if (string.IsNullOrWhiteSpace(indexPart))
            return false;

        return ushort.TryParse(indexPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out index);
    }
}
