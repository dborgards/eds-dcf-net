namespace EdsDcfNet;

using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Writers;
using System.Globalization;

/// <summary>
/// Main entry point for working with EDS and DCF files.
/// Provides a simple, fluent API for reading and writing CANopen configuration files.
/// </summary>
public static class CanOpenFile
{
    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) file.
    /// </summary>
    /// <param name="filePath">Path to the EDS file</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    /// <example>
    /// <code>
    /// var eds = CanOpenFile.ReadEds("device.eds");
    /// Console.WriteLine($"Device: {eds.DeviceInfo.ProductName}");
    /// </code>
    /// </example>
    public static ElectronicDataSheet ReadEds(string filePath)
    {
        var reader = new EdsReader();
        return reader.ReadFile(filePath);
    }

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) from a string.
    /// </summary>
    /// <param name="content">EDS file content as string</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadEdsFromString(string content)
    {
        var reader = new EdsReader();
        return reader.ReadString(content);
    }

    /// <summary>
    /// Reads a Device Configuration File (DCF).
    /// </summary>
    /// <param name="filePath">Path to the DCF file</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    /// <example>
    /// <code>
    /// var dcf = CanOpenFile.ReadDcf("device_node2.dcf");
    /// Console.WriteLine($"Node ID: {dcf.DeviceCommissioning.NodeId}");
    /// Console.WriteLine($"Baudrate: {dcf.DeviceCommissioning.Baudrate} kbit/s");
    /// </code>
    /// </example>
    public static DeviceConfigurationFile ReadDcf(string filePath)
    {
        var reader = new DcfReader();
        return reader.ReadFile(filePath);
    }

    /// <summary>
    /// Reads a Device Configuration File (DCF) from a string.
    /// </summary>
    /// <param name="content">DCF file content as string</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadDcfFromString(string content)
    {
        var reader = new DcfReader();
        return reader.ReadString(content);
    }

    /// <summary>
    /// Writes a Device Configuration File (DCF) to disk.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the DCF file should be written</param>
    /// <example>
    /// <code>
    /// var dcf = CanOpenFile.ReadDcf("template.dcf");
    /// dcf.DeviceCommissioning.NodeId = 5;
    /// dcf.DeviceCommissioning.Baudrate = 500;
    /// CanOpenFile.WriteDcf(dcf, "configured_device.dcf");
    /// </code>
    /// </example>
    public static void WriteDcf(DeviceConfigurationFile dcf, string filePath)
    {
        var writer = new DcfWriter();
        writer.WriteFile(dcf, filePath);
    }

    /// <summary>
    /// Generates a DCF file content as string.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to convert</param>
    /// <returns>DCF content as string</returns>
    public static string WriteDcfToString(DeviceConfigurationFile dcf)
    {
        var writer = new DcfWriter();
        return writer.GenerateString(dcf);
    }

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) file.
    /// </summary>
    /// <param name="filePath">Path to the CPJ file</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static NodelistProject ReadCpj(string filePath)
    {
        var reader = new CpjReader();
        return reader.ReadFile(filePath);
    }

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) from a string.
    /// </summary>
    /// <param name="content">CPJ file content as string</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static NodelistProject ReadCpjFromString(string content)
    {
        var reader = new CpjReader();
        return reader.ReadString(content);
    }

    /// <summary>
    /// Writes a CiA 306-3 nodelist project (.cpj) to disk.
    /// </summary>
    /// <param name="cpj">The NodelistProject to write</param>
    /// <param name="filePath">Path where the CPJ file should be written</param>
    public static void WriteCpj(NodelistProject cpj, string filePath)
    {
        var writer = new CpjWriter();
        writer.WriteFile(cpj, filePath);
    }

    /// <summary>
    /// Generates CPJ file content as string.
    /// </summary>
    /// <param name="cpj">The NodelistProject to convert</param>
    /// <returns>CPJ content as string</returns>
    public static string WriteCpjToString(NodelistProject cpj)
    {
        var writer = new CpjWriter();
        return writer.GenerateString(cpj);
    }

    /// <summary>
    /// Converts an EDS to a DCF with specified commissioning parameters.
    /// </summary>
    /// <param name="eds">The EDS to convert</param>
    /// <param name="nodeId">Node ID for the device</param>
    /// <param name="baudrate">Baudrate in kbit/s (default: 250)</param>
    /// <param name="nodeName">Optional node name</param>
    /// <returns>A new DeviceConfigurationFile</returns>
    /// <example>
    /// <code>
    /// var eds = CanOpenFile.ReadEds("device.eds");
    /// var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 2, baudrate: 500, nodeName: "MyDevice");
    /// CanOpenFile.WriteDcf(dcf, "device_node2.dcf");
    /// </code>
    /// </example>
    public static DeviceConfigurationFile EdsToDcf(
        ElectronicDataSheet eds,
        byte nodeId,
        ushort baudrate = 250,
        string? nodeName = null)
    {
        if (nodeId < 1 || nodeId > 127)
            throw new ArgumentOutOfRangeException(nameof(nodeId), nodeId, "CANopen Node-ID must be in range 1..127.");

        var now = DateTime.Now;
        var dcf = new DeviceConfigurationFile
        {
            FileInfo = new Models.EdsFileInfo
            {
                FileName = Path.ChangeExtension(eds.FileInfo.FileName, ".dcf"),
                FileVersion = eds.FileInfo.FileVersion,
                FileRevision = (byte)(eds.FileInfo.FileRevision + 1),
                EdsVersion = eds.FileInfo.EdsVersion,
                Description = $"DCF generated from {eds.FileInfo.FileName}",
                CreationDate = now.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture),
                CreationTime = now.ToString("hh:mmtt", CultureInfo.InvariantCulture),
                CreatedBy = "EdsDcfNet Library",
                LastEds = eds.FileInfo.FileName
            },
            DeviceInfo = CloneDeviceInfo(eds.DeviceInfo),
            DeviceCommissioning = new DeviceCommissioning
            {
                NodeId = nodeId,
                Baudrate = baudrate,
                NodeName = nodeName ?? $"{eds.DeviceInfo.ProductName}_Node{nodeId}",
                NetNumber = 1,
                NetworkName = "CANopen Network",
                CANopenManager = false
            },
            ObjectDictionary = CloneObjectDictionary(eds.ObjectDictionary),
            Comments = CloneComments(eds.Comments),
        };

        dcf.SupportedModules.AddRange(CloneSupportedModules(eds.SupportedModules));
        foreach (var kvp in CloneAdditionalSections(eds.AdditionalSections))
            dcf.AdditionalSections[kvp.Key] = kvp.Value;

        return dcf;
    }

    private static DeviceInfo CloneDeviceInfo(DeviceInfo source)
    {
        return new DeviceInfo
        {
            VendorName = source.VendorName,
            VendorNumber = source.VendorNumber,
            ProductName = source.ProductName,
            ProductNumber = source.ProductNumber,
            RevisionNumber = source.RevisionNumber,
            OrderCode = source.OrderCode,
            SupportedBaudRates = new BaudRates
            {
                BaudRate10 = source.SupportedBaudRates.BaudRate10,
                BaudRate20 = source.SupportedBaudRates.BaudRate20,
                BaudRate50 = source.SupportedBaudRates.BaudRate50,
                BaudRate125 = source.SupportedBaudRates.BaudRate125,
                BaudRate250 = source.SupportedBaudRates.BaudRate250,
                BaudRate500 = source.SupportedBaudRates.BaudRate500,
                BaudRate800 = source.SupportedBaudRates.BaudRate800,
                BaudRate1000 = source.SupportedBaudRates.BaudRate1000
            },
            SimpleBootUpMaster = source.SimpleBootUpMaster,
            SimpleBootUpSlave = source.SimpleBootUpSlave,
            Granularity = source.Granularity,
            DynamicChannelsSupported = source.DynamicChannelsSupported,
            GroupMessaging = source.GroupMessaging,
            NrOfRxPdo = source.NrOfRxPdo,
            NrOfTxPdo = source.NrOfTxPdo,
            LssSupported = source.LssSupported,
            CompactPdo = source.CompactPdo,
            CANopenSafetySupported = source.CANopenSafetySupported
        };
    }

    private static ObjectDictionary CloneObjectDictionary(ObjectDictionary source)
    {
        var clone = new ObjectDictionary();
        clone.MandatoryObjects.AddRange(source.MandatoryObjects);
        clone.OptionalObjects.AddRange(source.OptionalObjects);
        clone.ManufacturerObjects.AddRange(source.ManufacturerObjects);
        foreach (var kvp in source.DummyUsage)
            clone.DummyUsage[kvp.Key] = kvp.Value;

        foreach (var kvp in source.Objects)
        {
            clone.Objects[kvp.Key] = CloneObject(kvp.Value);
        }

        return clone;
    }

    private static CanOpenObject CloneObject(CanOpenObject source)
    {
        var clone = new CanOpenObject
        {
            Index = source.Index,
            ParameterName = source.ParameterName,
            ObjectType = source.ObjectType,
            DataType = source.DataType,
            AccessType = source.AccessType,
            DefaultValue = source.DefaultValue,
            LowLimit = source.LowLimit,
            HighLimit = source.HighLimit,
            PdoMapping = source.PdoMapping,
            ObjFlags = source.ObjFlags,
            SubNumber = source.SubNumber,
            CompactSubObj = source.CompactSubObj,
            ParameterValue = source.ParameterValue,
            Denotation = source.Denotation,
            UploadFile = source.UploadFile,
            DownloadFile = source.DownloadFile,
            SrdoMapping = source.SrdoMapping,
            InvertedSrad = source.InvertedSrad,
            ParamRefd = source.ParamRefd
        };

        clone.ObjectLinks.AddRange(source.ObjectLinks);

        foreach (var kvp in source.SubObjects)
        {
            clone.SubObjects[kvp.Key] = CloneSubObject(kvp.Value);
        }

        return clone;
    }

    private static CanOpenSubObject CloneSubObject(CanOpenSubObject source)
    {
        return new CanOpenSubObject
        {
            SubIndex = source.SubIndex,
            ParameterName = source.ParameterName,
            ObjectType = source.ObjectType,
            DataType = source.DataType,
            AccessType = source.AccessType,
            DefaultValue = source.DefaultValue,
            LowLimit = source.LowLimit,
            HighLimit = source.HighLimit,
            PdoMapping = source.PdoMapping,
            ParameterValue = source.ParameterValue,
            Denotation = source.Denotation,
            SrdoMapping = source.SrdoMapping,
            InvertedSrad = source.InvertedSrad,
            ParamRefd = source.ParamRefd
        };
    }

    private static Comments? CloneComments(Comments? source)
    {
        if (source == null) return null;
        var clone = new Comments { Lines = source.Lines };
        foreach (var kvp in source.CommentLines)
            clone.CommentLines[kvp.Key] = kvp.Value;
        return clone;
    }

    private static List<ModuleInfo> CloneSupportedModules(List<ModuleInfo> source)
    {
        var clone = new List<ModuleInfo>(source.Count);
        foreach (var module in source)
        {
            var clonedModule = new ModuleInfo
            {
                ModuleNumber = module.ModuleNumber,
                ProductName = module.ProductName,
                ProductVersion = module.ProductVersion,
                ProductRevision = module.ProductRevision,
                OrderCode = module.OrderCode,
                Comments = CloneComments(module.Comments)
            };

            clonedModule.FixedObjects.AddRange(module.FixedObjects);
            clonedModule.SubExtends.AddRange(module.SubExtends);

            foreach (var kvp in module.FixedObjectDefinitions)
            {
                clonedModule.FixedObjectDefinitions[kvp.Key] = CloneObject(kvp.Value);
            }

            foreach (var kvp in module.SubExtensionDefinitions)
            {
                clonedModule.SubExtensionDefinitions[kvp.Key] = new ModuleSubExtension
                {
                    Index = kvp.Value.Index,
                    ParameterName = kvp.Value.ParameterName,
                    DataType = kvp.Value.DataType,
                    AccessType = kvp.Value.AccessType,
                    DefaultValue = kvp.Value.DefaultValue,
                    PdoMapping = kvp.Value.PdoMapping,
                    Count = kvp.Value.Count,
                    ObjExtend = kvp.Value.ObjExtend
                };
            }

            clone.Add(clonedModule);
        }
        return clone;
    }

    private static Dictionary<string, Dictionary<string, string>> CloneAdditionalSections(
        Dictionary<string, Dictionary<string, string>> source)
    {
        var clone = new Dictionary<string, Dictionary<string, string>>(source.Count);
        foreach (var kvp in source)
        {
            clone[kvp.Key] = new Dictionary<string, string>(kvp.Value);
        }
        return clone;
    }
}
