namespace EdsDcfNet.Writers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Writer for Electronic Data Sheet (EDS) files.
/// </summary>
public class EdsWriter
{
    /// <summary>
    /// Writes an EDS to the specified file path.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the EDS file should be written</param>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public void WriteFile(ElectronicDataSheet eds, string filePath)
    {
        try
        {
            var content = GenerateEdsContent(eds);
            File.WriteAllText(filePath, content, TextFileIo.Utf8NoBom);
        }
        catch (EdsWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new EdsWriteException($"Failed to write EDS file to {filePath}", ex);
        }
    }

    /// <summary>
    /// Writes an EDS to the specified file path asynchronously.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the EDS file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public async Task WriteFileAsync(
        ElectronicDataSheet eds,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = GenerateEdsContent(eds);
            await TextFileIo.WriteAllTextAsync(filePath, content, TextFileIo.Utf8NoBom, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EdsWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new EdsWriteException($"Failed to write EDS file to {filePath}", ex);
        }
    }

    /// <summary>
    /// Generates EDS content as a string.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to convert</param>
    /// <returns>EDS content as string</returns>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public string GenerateString(ElectronicDataSheet eds)
    {
        return GenerateEdsContent(eds);
    }

    private static string GenerateEdsContent(ElectronicDataSheet eds)
    {
        var sb = new StringBuilder();

        // Write FileInfo section
        WriteSection("FileInfo", () => WriteFileInfo(sb, eds.FileInfo));

        // Write DeviceInfo section
        WriteSection("DeviceInfo", () => WriteDeviceInfo(sb, eds.DeviceInfo));

        // Write DummyUsage section if present
        if (eds.ObjectDictionary.DummyUsage.Count > 0)
        {
            WriteSection("DummyUsage", () => WriteDummyUsage(sb, eds.ObjectDictionary));
        }

        // Write object lists
        WriteSection("ObjectLists", () => WriteObjectLists(sb, eds.ObjectDictionary));

        // Write all objects
        WriteSection("Objects", () => WriteObjects(sb, eds.ObjectDictionary));

        // Write SupportedModules if present
        if (eds.SupportedModules.Count > 0)
        {
            WriteSection("SupportedModules", () => WriteSupportedModules(sb, eds.SupportedModules));
        }

        // Write DynamicChannels if present
        if (eds.DynamicChannels != null && eds.DynamicChannels.Segments.Count > 0)
        {
            WriteSection("DynamicChannels", () => WriteDynamicChannels(sb, eds.DynamicChannels));
        }

        // Write Tools if present
        if (eds.Tools.Count > 0)
        {
            WriteSection("Tools", () => WriteTools(sb, eds.Tools));
        }

        // Write Comments section if present
        if (eds.Comments != null && eds.Comments.CommentLines.Count > 0)
        {
            WriteSection("Comments", () => WriteComments(sb, eds.Comments));
        }

        // Write additional sections
        foreach (var section in eds.AdditionalSections)
        {
            if (ObjectLinksSectionHelper.IsObjectLinksSectionForExistingObject(section.Key, eds.ObjectDictionary))
            {
                continue;
            }

            WriteSection(section.Key, () => WriteAdditionalSection(sb, section.Key, section.Value));
        }

        return sb.ToString();
    }

    private static void WriteFileInfo(StringBuilder sb, Models.EdsFileInfo fileInfo)
    {
        sb.AppendLine("[FileInfo]");
        WriteKeyValue(sb, "FileName", fileInfo.FileName);
        WriteKeyValue(sb, "FileVersion", fileInfo.FileVersion.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "FileRevision", fileInfo.FileRevision.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "EDSVersion", fileInfo.EdsVersion);
        WriteKeyValue(sb, "Description", fileInfo.Description);
        WriteKeyValue(sb, "CreationTime", fileInfo.CreationTime);
        WriteKeyValue(sb, "CreationDate", fileInfo.CreationDate);
        WriteKeyValue(sb, "CreatedBy", fileInfo.CreatedBy);
        WriteKeyValue(sb, "ModificationTime", fileInfo.ModificationTime);
        WriteKeyValue(sb, "ModificationDate", fileInfo.ModificationDate);
        WriteKeyValue(sb, "ModifiedBy", fileInfo.ModifiedBy);

        sb.AppendLine();
    }

    private static void WriteDeviceInfo(StringBuilder sb, DeviceInfo deviceInfo)
    {
        sb.AppendLine("[DeviceInfo]");
        WriteKeyValue(sb, "VendorName", deviceInfo.VendorName);
        WriteKeyValue(sb, "VendorNumber", ValueConverter.FormatInteger(deviceInfo.VendorNumber));
        WriteKeyValue(sb, "ProductName", deviceInfo.ProductName);
        WriteKeyValue(sb, "ProductNumber", ValueConverter.FormatInteger(deviceInfo.ProductNumber));
        WriteKeyValue(sb, "RevisionNumber", ValueConverter.FormatInteger(deviceInfo.RevisionNumber));
        WriteKeyValue(sb, "OrderCode", deviceInfo.OrderCode);

        WriteKeyValue(sb, "BaudRate_10", ValueConverter.FormatBoolean(deviceInfo.SupportedBaudRates.BaudRate10));
        WriteKeyValue(sb, "BaudRate_20", ValueConverter.FormatBoolean(deviceInfo.SupportedBaudRates.BaudRate20));
        WriteKeyValue(sb, "BaudRate_50", ValueConverter.FormatBoolean(deviceInfo.SupportedBaudRates.BaudRate50));
        WriteKeyValue(sb, "BaudRate_125", ValueConverter.FormatBoolean(deviceInfo.SupportedBaudRates.BaudRate125));
        WriteKeyValue(sb, "BaudRate_250", ValueConverter.FormatBoolean(deviceInfo.SupportedBaudRates.BaudRate250));
        WriteKeyValue(sb, "BaudRate_500", ValueConverter.FormatBoolean(deviceInfo.SupportedBaudRates.BaudRate500));
        WriteKeyValue(sb, "BaudRate_800", ValueConverter.FormatBoolean(deviceInfo.SupportedBaudRates.BaudRate800));
        WriteKeyValue(sb, "BaudRate_1000", ValueConverter.FormatBoolean(deviceInfo.SupportedBaudRates.BaudRate1000));

        WriteKeyValue(sb, "SimpleBootUpMaster", ValueConverter.FormatBoolean(deviceInfo.SimpleBootUpMaster));
        WriteKeyValue(sb, "SimpleBootUpSlave", ValueConverter.FormatBoolean(deviceInfo.SimpleBootUpSlave));
        WriteKeyValue(sb, "Granularity", deviceInfo.Granularity.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "DynamicChannelsSupported", deviceInfo.DynamicChannelsSupported.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "GroupMessaging", ValueConverter.FormatBoolean(deviceInfo.GroupMessaging));
        WriteKeyValue(sb, "NrOfRXPDO", deviceInfo.NrOfRxPdo.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "NrOfTXPDO", deviceInfo.NrOfTxPdo.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "LSS_Supported", ValueConverter.FormatBoolean(deviceInfo.LssSupported));

        if (deviceInfo.CompactPdo > 0)
        {
            WriteKeyValue(sb, "CompactPDO", ValueConverter.FormatInteger(deviceInfo.CompactPdo));
        }

        if (deviceInfo.CANopenSafetySupported)
        {
            WriteKeyValue(sb, "CANopenSafetySupported", ValueConverter.FormatBoolean(deviceInfo.CANopenSafetySupported));
        }

        sb.AppendLine();
    }

    private static void WriteDummyUsage(StringBuilder sb, ObjectDictionary objDict)
    {
        sb.AppendLine("[DummyUsage]");

        foreach (var dummy in objDict.DummyUsage.OrderBy(d => d.Key))
        {
            WriteKeyValue(sb, string.Format(CultureInfo.InvariantCulture, "Dummy{0:X4}", dummy.Key), ValueConverter.FormatBoolean(dummy.Value));
        }

        sb.AppendLine();
    }

    private static void WriteObjectLists(StringBuilder sb, ObjectDictionary objDict)
    {
        // Write MandatoryObjects
        if (objDict.MandatoryObjects.Count > 0)
        {
            sb.AppendLine("[MandatoryObjects]");
            WriteKeyValue(sb, "SupportedObjects", objDict.MandatoryObjects.Count.ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < objDict.MandatoryObjects.Count; i++)
            {
                WriteKeyValue(sb, (i + 1).ToString(CultureInfo.InvariantCulture), ValueConverter.FormatInteger(objDict.MandatoryObjects[i]));
            }

            sb.AppendLine();
        }

        // Write OptionalObjects
        if (objDict.OptionalObjects.Count > 0)
        {
            sb.AppendLine("[OptionalObjects]");
            WriteKeyValue(sb, "SupportedObjects", objDict.OptionalObjects.Count.ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < objDict.OptionalObjects.Count; i++)
            {
                WriteKeyValue(sb, (i + 1).ToString(CultureInfo.InvariantCulture), ValueConverter.FormatInteger(objDict.OptionalObjects[i]));
            }

            sb.AppendLine();
        }

        // Write ManufacturerObjects
        if (objDict.ManufacturerObjects.Count > 0)
        {
            sb.AppendLine("[ManufacturerObjects]");
            WriteKeyValue(sb, "SupportedObjects", objDict.ManufacturerObjects.Count.ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < objDict.ManufacturerObjects.Count; i++)
            {
                WriteKeyValue(sb, (i + 1).ToString(CultureInfo.InvariantCulture), ValueConverter.FormatInteger(objDict.ManufacturerObjects[i]));
            }

            sb.AppendLine();
        }
    }

    private static void WriteObjects(StringBuilder sb, ObjectDictionary objDict)
    {
        var allObjects = objDict.Objects.OrderBy(o => o.Key);

        foreach (var objEntry in allObjects)
        {
            var sectionName = string.Format(CultureInfo.InvariantCulture, "{0:X}", objEntry.Key);
            WriteSection(sectionName, () => WriteObject(sb, objEntry.Value));
        }
    }

    private static void WriteObject(StringBuilder sb, CanOpenObject obj)
    {
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[{0:X}]", obj.Index));

        if (obj.SubNumber.HasValue && obj.SubNumber.Value > 0)
        {
            WriteKeyValue(sb, "SubNumber", obj.SubNumber.Value.ToString(CultureInfo.InvariantCulture));
        }

        WriteKeyValue(sb, "ParameterName", obj.ParameterName);
        WriteKeyValue(sb, "ObjectType", ValueConverter.FormatInteger(obj.ObjectType));

        if (obj.DataType.HasValue)
        {
            WriteKeyValue(sb, "DataType", ValueConverter.FormatInteger(obj.DataType.Value));
        }

        WriteKeyValue(sb, "AccessType", ValueConverter.AccessTypeToString(obj.AccessType));

        if (!string.IsNullOrEmpty(obj.DefaultValue))
        {
            WriteKeyValue(sb, "DefaultValue", obj.DefaultValue);
        }

        if (!string.IsNullOrEmpty(obj.LowLimit))
        {
            WriteKeyValue(sb, "LowLimit", obj.LowLimit);
        }

        if (!string.IsNullOrEmpty(obj.HighLimit))
        {
            WriteKeyValue(sb, "HighLimit", obj.HighLimit);
        }

        WriteKeyValue(sb, "PDOMapping", ValueConverter.FormatBoolean(obj.PdoMapping));

        if (obj.SrdoMapping)
        {
            WriteKeyValue(sb, "SRDOMapping", ValueConverter.FormatBoolean(obj.SrdoMapping));
        }

        if (!string.IsNullOrEmpty(obj.InvertedSrad))
        {
            WriteKeyValue(sb, "InvertedSRAD", obj.InvertedSrad);
        }

        if (obj.ObjFlags > 0)
        {
            WriteKeyValue(sb, "ObjFlags", ValueConverter.FormatInteger(obj.ObjFlags));
        }

        if (obj.CompactSubObj.HasValue && obj.CompactSubObj.Value > 0)
        {
            WriteKeyValue(sb, "CompactSubObj", obj.CompactSubObj.Value.ToString(CultureInfo.InvariantCulture));
        }

        sb.AppendLine();

        // Write sub-objects
        if (obj.SubObjects.Count > 0)
        {
            foreach (var subObjEntry in obj.SubObjects.OrderBy(s => s.Key))
            {
                var sectionName = string.Format(CultureInfo.InvariantCulture, "{0:X}sub{1:X}", obj.Index, subObjEntry.Key);
                WriteSection(sectionName, () => WriteSubObject(sb, obj.Index, subObjEntry.Value));
            }
        }

        // Write object links
        if (obj.ObjectLinks.Count > 0)
        {
            WriteSection(
                string.Format(CultureInfo.InvariantCulture, "{0:X}ObjectLinks", obj.Index),
                () =>
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[{0:X}ObjectLinks]", obj.Index));
                    WriteKeyValue(sb, "ObjectLinks", obj.ObjectLinks.Count.ToString(CultureInfo.InvariantCulture));

                    for (int i = 0; i < obj.ObjectLinks.Count; i++)
                    {
                        WriteKeyValue(sb, (i + 1).ToString(CultureInfo.InvariantCulture), ValueConverter.FormatInteger(obj.ObjectLinks[i]));
                    }

                    sb.AppendLine();
                });
        }
    }

    private static void WriteSubObject(StringBuilder sb, ushort index, CanOpenSubObject subObj)
    {
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[{0:X}sub{1:X}]", index, subObj.SubIndex));

        WriteKeyValue(sb, "ParameterName", subObj.ParameterName);
        WriteKeyValue(sb, "ObjectType", ValueConverter.FormatInteger(subObj.ObjectType));
        WriteKeyValue(sb, "DataType", ValueConverter.FormatInteger(subObj.DataType));
        WriteKeyValue(sb, "AccessType", ValueConverter.AccessTypeToString(subObj.AccessType));

        if (!string.IsNullOrEmpty(subObj.DefaultValue))
        {
            WriteKeyValue(sb, "DefaultValue", subObj.DefaultValue);
        }

        if (!string.IsNullOrEmpty(subObj.LowLimit))
        {
            WriteKeyValue(sb, "LowLimit", subObj.LowLimit);
        }

        if (!string.IsNullOrEmpty(subObj.HighLimit))
        {
            WriteKeyValue(sb, "HighLimit", subObj.HighLimit);
        }

        WriteKeyValue(sb, "PDOMapping", ValueConverter.FormatBoolean(subObj.PdoMapping));

        if (subObj.SrdoMapping)
        {
            WriteKeyValue(sb, "SRDOMapping", ValueConverter.FormatBoolean(subObj.SrdoMapping));
        }

        if (!string.IsNullOrEmpty(subObj.InvertedSrad))
        {
            WriteKeyValue(sb, "InvertedSRAD", subObj.InvertedSrad);
        }

        sb.AppendLine();
    }

    private static void WriteSupportedModules(StringBuilder sb, List<ModuleInfo> modules)
    {
        sb.AppendLine("[SupportedModules]");
        WriteKeyValue(sb, "NrOfEntries", modules.Count.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();

        foreach (var module in modules)
        {
            WriteModuleInfo(sb, module);
        }
    }

    private static void WriteModuleInfo(StringBuilder sb, ModuleInfo module)
    {
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[M{0}ModuleInfo]", module.ModuleNumber));
        WriteKeyValue(sb, "ProductName", module.ProductName);
        WriteKeyValue(sb, "ProductVersion", module.ProductVersion.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "ProductRevision", module.ProductRevision.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "OrderCode", module.OrderCode);
        sb.AppendLine();

        if (module.FixedObjects.Count > 0)
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[M{0}FixedObjects]", module.ModuleNumber));
            WriteKeyValue(sb, "NrOfEntries", module.FixedObjects.Count.ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < module.FixedObjects.Count; i++)
            {
                WriteKeyValue(sb, (i + 1).ToString(CultureInfo.InvariantCulture), ValueConverter.FormatInteger(module.FixedObjects[i]));
            }

            sb.AppendLine();
        }
    }

    private static void WriteComments(StringBuilder sb, Comments comments)
    {
        sb.AppendLine("[Comments]");
        WriteKeyValue(sb, "Lines", comments.Lines.ToString(CultureInfo.InvariantCulture));

        foreach (var line in comments.CommentLines.OrderBy(l => l.Key))
        {
            WriteKeyValue(sb, string.Format(CultureInfo.InvariantCulture, "Line{0}", line.Key), line.Value);
        }

        sb.AppendLine();
    }

    private static void WriteDynamicChannels(StringBuilder sb, DynamicChannels dynamicChannels)
    {
        sb.AppendLine("[DynamicChannels]");
        WriteKeyValue(sb, "NrOfSeg", dynamicChannels.Segments.Count.ToString(CultureInfo.InvariantCulture));

        for (int i = 0; i < dynamicChannels.Segments.Count; i++)
        {
            var idx = (i + 1).ToString(CultureInfo.InvariantCulture);
            var seg = dynamicChannels.Segments[i];
            WriteKeyValue(sb, $"Type{idx}", ValueConverter.FormatInteger(seg.Type));
            WriteKeyValue(sb, $"Dir{idx}", ValueConverter.AccessTypeToString(seg.Dir));
            WriteKeyValue(sb, $"Range{idx}", seg.Range);
            WriteKeyValue(sb, $"PPOffset{idx}", seg.PPOffset.ToString(CultureInfo.InvariantCulture));
        }

        sb.AppendLine();
    }

    private static void WriteTools(StringBuilder sb, List<ToolInfo> tools)
    {
        sb.AppendLine("[Tools]");
        WriteKeyValue(sb, "Items", tools.Count.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();

        for (int i = 0; i < tools.Count; i++)
        {
            var idx = (i + 1).ToString(CultureInfo.InvariantCulture);
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[Tool{0}]", idx));
            WriteKeyValue(sb, "Name", tools[i].Name);
            WriteKeyValue(sb, "Command", tools[i].Command);
            sb.AppendLine();
        }
    }

    private static void WriteAdditionalSection(StringBuilder sb, string sectionName, Dictionary<string, string> entries)
    {
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[{0}]", sectionName));

        foreach (var entry in entries)
        {
            WriteKeyValue(sb, entry.Key, entry.Value);
        }

        sb.AppendLine();
    }

    private static void WriteKeyValue(StringBuilder sb, string key, string? value)
    {
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}={1}", key, value));
    }

    private static void WriteSection(string sectionName, Action writeAction)
    {
        try
        {
            writeAction();
        }
        catch (EdsWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new EdsWriteException(
                $"Failed to write section [{sectionName}]",
                ex)
            {
                SectionName = sectionName
            };
        }
    }
}
