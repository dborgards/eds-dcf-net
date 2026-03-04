namespace EdsDcfNet.Utilities;

using System.Globalization;
using System.Text;
using EdsDcfNet.Models;

internal static class ObjectListSectionWriter
{
    public static void WriteObjectLists(
        StringBuilder sb,
        ObjectDictionary objDict,
        Action<StringBuilder, string, string?> writeKeyValue)
    {
        WriteObjectListSection(sb, "MandatoryObjects", objDict.MandatoryObjects, writeKeyValue);
        WriteObjectListSection(sb, "OptionalObjects", objDict.OptionalObjects, writeKeyValue);
        WriteObjectListSection(sb, "ManufacturerObjects", objDict.ManufacturerObjects, writeKeyValue);
    }

    private static void WriteObjectListSection(
        StringBuilder sb,
        string sectionName,
        List<ushort> objectIndexes,
        Action<StringBuilder, string, string?> writeKeyValue)
    {
        if (objectIndexes.Count == 0)
            return;

        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[{0}]", sectionName));
        writeKeyValue(sb, "SupportedObjects", objectIndexes.Count.ToString(CultureInfo.InvariantCulture));

        for (int i = 0; i < objectIndexes.Count; i++)
        {
            writeKeyValue(
                sb,
                (i + 1).ToString(CultureInfo.InvariantCulture),
                ValueConverter.FormatInteger(objectIndexes[i]));
        }

        sb.AppendLine();
    }
}
