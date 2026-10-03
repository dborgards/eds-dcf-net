namespace EdsDcfNet.Utilities;

using System.Globalization;
using System.Text;
using EdsDcfNet.Models;
using EdsDcfNet.Writers;

internal static class ObjectListSectionWriter
{
    public static void WriteObjectLists(
        StringBuilder sb,
        ObjectDictionary objDict,
        Action<StringBuilder, string, string?> writeKeyValue,
        Dictionary<string, OrderedStringDictionary>? sectionEntries)
    {
        WriteObjectListSection(sb, "MandatoryObjects", objDict.MandatoryObjects, writeKeyValue, sectionEntries);
        WriteObjectListSection(sb, "OptionalObjects", objDict.OptionalObjects, writeKeyValue, sectionEntries);
        WriteObjectListSection(sb, "ManufacturerObjects", objDict.ManufacturerObjects, writeKeyValue, sectionEntries);
    }

    /// <summary>
    /// Writes one object list (CiA 306-1 § 6.6.3.1), then the kept entries of that section in
    /// file order. A kept numbered entry that the writer now generates itself (the caller added
    /// objects) is dropped, so each number appears once.
    /// </summary>
    private static void WriteObjectListSection(
        StringBuilder sb,
        string sectionName,
        List<ushort> objectIndexes,
        Action<StringBuilder, string, string?> writeKeyValue,
        Dictionary<string, OrderedStringDictionary>? sectionEntries)
    {
        OrderedStringDictionary? kept = null;
        if (sectionEntries != null
            && sectionEntries.TryGetValue(sectionName, out var entries)
            && entries != null
            && entries.Count > 0)
        {
            kept = entries;
        }

        if (objectIndexes.Count == 0 && kept == null)
            return;

        IniRoundTripText.WriteSectionHeader(sb, sectionName);
        writeKeyValue(sb, SectionEntryKeys.SupportedObjectsKey, objectIndexes.Count.ToString(CultureInfo.InvariantCulture));

        for (int i = 0; i < objectIndexes.Count; i++)
        {
            writeKeyValue(
                sb,
                (i + 1).ToString(CultureInfo.InvariantCulture),
                ValueConverter.FormatInteger(objectIndexes[i]));
        }

        if (kept != null)
        {
            foreach (var entry in kept)
            {
                if (SectionEntryKeys.IsCountedListKey(entry.Key, SectionEntryKeys.SupportedObjectsKey, objectIndexes.Count))
                    continue;

                writeKeyValue(sb, entry.Key, entry.Value);
            }
        }

        sb.AppendLine();
    }
}
