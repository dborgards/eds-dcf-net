namespace EdsDcfNet;

using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Writers;

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
        var dcf = new DeviceConfigurationFile
        {
            FileInfo = new Models.FileInfo
            {
                FileName = eds.FileInfo.FileName.Replace(".eds", ".dcf", StringComparison.OrdinalIgnoreCase),
                FileVersion = eds.FileInfo.FileVersion,
                FileRevision = (byte)(eds.FileInfo.FileRevision + 1),
                EdsVersion = eds.FileInfo.EdsVersion,
                Description = $"DCF generated from {eds.FileInfo.FileName}",
                CreationDate = DateTime.Now.ToString("MM-dd-yyyy"),
                CreationTime = DateTime.Now.ToString("hh:mmtt"),
                CreatedBy = "EdsDcfNet Library",
                LastEds = eds.FileInfo.FileName
            },
            DeviceInfo = eds.DeviceInfo,
            DeviceCommissioning = new DeviceCommissioning
            {
                NodeId = nodeId,
                Baudrate = baudrate,
                NodeName = nodeName ?? $"{eds.DeviceInfo.ProductName}_Node{nodeId}",
                NetNumber = 1,
                NetworkName = "CANopen Network",
                CANopenManager = false
            },
            ObjectDictionary = eds.ObjectDictionary,
            Comments = eds.Comments,
            SupportedModules = eds.SupportedModules,
            AdditionalSections = new Dictionary<string, Dictionary<string, string>>(eds.AdditionalSections)
        };

        return dcf;
    }
}
