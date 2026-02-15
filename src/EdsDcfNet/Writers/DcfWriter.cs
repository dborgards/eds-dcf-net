namespace EdsDcfNet.Writers;

using System.Globalization;
using System.Text;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Writer for Device Configuration File (DCF) files.
/// </summary>
public class DcfWriter
{
    /// <summary>
    /// Writes a DCF to the specified file path.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the DCF file should be written</param>
    public void WriteFile(DeviceConfigurationFile dcf, string filePath)
    {
        try
        {
            var content = GenerateDcfContent(dcf);
            File.WriteAllText(filePath, content, Encoding.ASCII);
        }
        catch (Exception ex)
        {
            throw new DcfWriteException($"Failed to write DCF file to {filePath}", ex);
        }
    }

    /// <summary>
    /// Generates DCF content as a string.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to convert</param>
    /// <returns>DCF content as string</returns>
    public string GenerateString(DeviceConfigurationFile dcf)
    {
        return GenerateDcfContent(dcf);
    }

    private string GenerateDcfContent(DeviceConfigurationFile dcf)
    {
        var sb = new StringBuilder();

        // Write FileInfo section
        WriteFileInfo(sb, dcf.FileInfo);

        // Write DeviceInfo section
        WriteDeviceInfo(sb, dcf.DeviceInfo);

        // Write DeviceCommissioning section
        WriteDeviceCommissioning(sb, dcf.DeviceCommissioning);

        // Write DummyUsage section if present
        if (dcf.ObjectDictionary.DummyUsage.Any())
        {
            WriteDummyUsage(sb, dcf.ObjectDictionary);
        }

        // Write object lists
        WriteObjectLists(sb, dcf.ObjectDictionary);

        // Write all objects
        WriteObjects(sb, dcf.ObjectDictionary);

        // Write SupportedModules if present
        if (dcf.SupportedModules.Any())
        {
            WriteSupportedModules(sb, dcf.SupportedModules);
        }

        // Write ConnectedModules if present
        if (dcf.ConnectedModules.Any())
        {
            WriteConnectedModules(sb, dcf.ConnectedModules);
        }

        // Write Comments section if present
        if (dcf.Comments != null && dcf.Comments.CommentLines.Any())
        {
            WriteComments(sb, dcf.Comments);
        }

        // Write additional sections
        foreach (var section in dcf.AdditionalSections)
        {
            WriteAdditionalSection(sb, section.Key, section.Value);
        }

        return sb.ToString();
    }

    private void WriteFileInfo(StringBuilder sb, Models.EdsFileInfo fileInfo)
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

        if (!string.IsNullOrEmpty(fileInfo.LastEds))
        {
            WriteKeyValue(sb, "LastEDS", fileInfo.LastEds);
        }

        sb.AppendLine();
    }

    private void WriteDeviceInfo(StringBuilder sb, DeviceInfo deviceInfo)
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

    private void WriteDeviceCommissioning(StringBuilder sb, DeviceCommissioning dc)
    {
        sb.AppendLine("[DeviceCommissioning]");
        WriteKeyValue(sb, "NodeID", dc.NodeId.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "NodeName", dc.NodeName);
        WriteKeyValue(sb, "Baudrate", dc.Baudrate.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "NetNumber", dc.NetNumber.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "NetworkName", dc.NetworkName);
        WriteKeyValue(sb, "CANopenManager", ValueConverter.FormatBoolean(dc.CANopenManager));

        if (dc.LssSerialNumber.HasValue)
        {
            WriteKeyValue(sb, "LSS_SerialNumber", dc.LssSerialNumber.Value.ToString(CultureInfo.InvariantCulture));
        }

        sb.AppendLine();
    }

    private void WriteDummyUsage(StringBuilder sb, ObjectDictionary objDict)
    {
        sb.AppendLine("[DummyUsage]");

        foreach (var dummy in objDict.DummyUsage.OrderBy(d => d.Key))
        {
            WriteKeyValue(sb, $"Dummy{dummy.Key:X4}", ValueConverter.FormatBoolean(dummy.Value));
        }

        sb.AppendLine();
    }

    private void WriteObjectLists(StringBuilder sb, ObjectDictionary objDict)
    {
        // Write MandatoryObjects
        if (objDict.MandatoryObjects.Any())
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
        if (objDict.OptionalObjects.Any())
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
        if (objDict.ManufacturerObjects.Any())
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

    private void WriteObjects(StringBuilder sb, ObjectDictionary objDict)
    {
        var allObjects = objDict.Objects.OrderBy(o => o.Key);

        foreach (var objEntry in allObjects)
        {
            WriteObject(sb, objEntry.Value);
        }
    }

    private void WriteObject(StringBuilder sb, CanOpenObject obj)
    {
        sb.AppendLine($"[{obj.Index:X}]");

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
        WriteKeyValue(sb, "SRDOMapping", ValueConverter.FormatBoolean(obj.SrdoMapping));

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

        // DCF-specific fields
        if (!string.IsNullOrEmpty(obj.ParameterValue))
        {
            WriteKeyValue(sb, "ParameterValue", obj.ParameterValue);
        }

        if (!string.IsNullOrEmpty(obj.Denotation))
        {
            WriteKeyValue(sb, "Denotation", obj.Denotation);
        }

        if (!string.IsNullOrEmpty(obj.UploadFile))
        {
            WriteKeyValue(sb, "UploadFile", obj.UploadFile);
        }

        if (!string.IsNullOrEmpty(obj.DownloadFile))
        {
            WriteKeyValue(sb, "DownloadFile", obj.DownloadFile);
        }

        sb.AppendLine();

        // Write sub-objects
        if (obj.SubObjects.Any())
        {
            foreach (var subObjEntry in obj.SubObjects.OrderBy(s => s.Key))
            {
                WriteSubObject(sb, obj.Index, subObjEntry.Value);
            }
        }

        // Write object links
        if (obj.ObjectLinks.Any())
        {
            sb.AppendLine($"[{obj.Index:X}ObjectLinks]");
            WriteKeyValue(sb, "ObjectLinks", obj.ObjectLinks.Count.ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < obj.ObjectLinks.Count; i++)
            {
                WriteKeyValue(sb, (i + 1).ToString(CultureInfo.InvariantCulture), ValueConverter.FormatInteger(obj.ObjectLinks[i]));
            }

            sb.AppendLine();
        }
    }

    private void WriteSubObject(StringBuilder sb, ushort index, CanOpenSubObject subObj)
    {
        sb.AppendLine($"[{index:X}sub{subObj.SubIndex:X}]");

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
        WriteKeyValue(sb, "SRDOMapping", ValueConverter.FormatBoolean(subObj.SrdoMapping));

        if (!string.IsNullOrEmpty(subObj.InvertedSrad))
        {
            WriteKeyValue(sb, "InvertedSRAD", subObj.InvertedSrad);
        }

        // DCF-specific fields
        if (!string.IsNullOrEmpty(subObj.ParameterValue))
        {
            WriteKeyValue(sb, "ParameterValue", subObj.ParameterValue);
        }

        if (!string.IsNullOrEmpty(subObj.Denotation))
        {
            WriteKeyValue(sb, "Denotation", subObj.Denotation);
        }

        sb.AppendLine();
    }

    private void WriteSupportedModules(StringBuilder sb, List<ModuleInfo> modules)
    {
        sb.AppendLine("[SupportedModules]");
        WriteKeyValue(sb, "NrOfEntries", modules.Count.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();

        foreach (var module in modules)
        {
            WriteModuleInfo(sb, module);
        }
    }

    private void WriteModuleInfo(StringBuilder sb, ModuleInfo module)
    {
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[M{0}ModuleInfo]", module.ModuleNumber));
        WriteKeyValue(sb, "ProductName", module.ProductName);
        WriteKeyValue(sb, "ProductVersion", module.ProductVersion.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "ProductRevision", module.ProductRevision.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "OrderCode", module.OrderCode);
        sb.AppendLine();

        if (module.FixedObjects.Any())
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

    private void WriteConnectedModules(StringBuilder sb, List<int> connectedModules)
    {
        sb.AppendLine("[ConnectedModules]");
        WriteKeyValue(sb, "NrOfEntries", connectedModules.Count.ToString(CultureInfo.InvariantCulture));

        for (int i = 0; i < connectedModules.Count; i++)
        {
            WriteKeyValue(sb, (i + 1).ToString(CultureInfo.InvariantCulture), connectedModules[i].ToString(CultureInfo.InvariantCulture));
        }

        sb.AppendLine();
    }

    private void WriteComments(StringBuilder sb, Comments comments)
    {
        sb.AppendLine("[Comments]");
        WriteKeyValue(sb, "Lines", comments.Lines.ToString(CultureInfo.InvariantCulture));

        foreach (var line in comments.CommentLines.OrderBy(l => l.Key))
        {
            WriteKeyValue(sb, $"Line{line.Key}", line.Value);
        }

        sb.AppendLine();
    }

    private void WriteAdditionalSection(StringBuilder sb, string sectionName, Dictionary<string, string> entries)
    {
        sb.AppendLine($"[{sectionName}]");

        foreach (var entry in entries)
        {
            WriteKeyValue(sb, entry.Key, entry.Value);
        }

        sb.AppendLine();
    }

    private void WriteKeyValue(StringBuilder sb, string key, string value)
    {
        sb.AppendLine($"{key}={value}");
    }
}
