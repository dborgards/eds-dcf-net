namespace EdsDcfNet;

using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Utilities;
using EdsDcfNet.Validation;
using EdsDcfNet.Writers;
using System.Globalization;

/// <summary>
/// Main entry point for working with EDS and DCF files.
/// Provides a simple, fluent API for reading and writing CANopen configuration files.
/// </summary>
public static class CanOpenFile
{
    /// <summary>
    /// Validates an Electronic Data Sheet (EDS) model using the full
    /// <see cref="CanOpenModelValidator"/> rule set.
    /// </summary>
    /// <param name="eds">Model instance to validate</param>
    /// <returns>List of validation issues. Empty when model is valid.</returns>
    public static IReadOnlyList<ValidationIssue> Validate(ElectronicDataSheet eds)
    {
        return CanOpenModelValidator.Validate(eds);
    }

    /// <summary>
    /// Validates a Device Configuration File (DCF) model using the full
    /// <see cref="CanOpenModelValidator"/> rule set.
    /// </summary>
    /// <param name="dcf">Model instance to validate</param>
    /// <remarks>
    /// For commissioning values, <c>NodeId == 0</c> and <c>Baudrate == 0</c>
    /// are treated as "unconfigured" and are accepted by validation.
    /// </remarks>
    /// <returns>List of validation issues. Empty when model is valid.</returns>
    public static IReadOnlyList<ValidationIssue> Validate(DeviceConfigurationFile dcf)
    {
        return CanOpenModelValidator.Validate(dcf);
    }

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) file.
    /// </summary>
    /// <param name="filePath">Path to the EDS file</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadEds(string filePath)
    {
        return ReadEds(filePath, IniParser.DefaultMaxInputSize);
    }

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) file.
    /// </summary>
    /// <param name="filePath">Path to the EDS file</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    /// <example>
    /// <code>
    /// var eds = CanOpenFile.ReadEds("device.eds");
    /// Console.WriteLine($"Device: {eds.DeviceInfo.ProductName}");
    /// </code>
    /// </example>
    public static ElectronicDataSheet ReadEds(
        string filePath,
        long maxInputSize)
    {
        var reader = new EdsReader();
        return reader.ReadFile(filePath, maxInputSize);
    }

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the EDS file</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadEdsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var reader = new EdsReader();
        return reader.ReadFileAsync(filePath, cancellationToken);
    }

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the EDS file</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadEdsAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var reader = new EdsReader();
        return reader.ReadFileAsync(filePath, maxInputSize, cancellationToken);
    }

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) from a string.
    /// </summary>
    /// <param name="content">EDS file content as string</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadEdsFromString(string content)
    {
        return ReadEdsFromString(content, IniParser.DefaultMaxInputSize);
    }

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) from a string.
    /// </summary>
    /// <param name="content">EDS file content as string</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadEdsFromString(
        string content,
        long maxInputSize)
    {
        var reader = new EdsReader();
        return reader.ReadString(content, maxInputSize);
    }

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing EDS content.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadEds(Stream stream)
    {
        return ReadEds(stream, IniParser.DefaultMaxInputSize);
    }

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing EDS content.</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadEds(
        Stream stream,
        long maxInputSize)
    {
        var reader = new EdsReader();
        return reader.ReadStream(stream, maxInputSize);
    }

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing EDS content.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadEdsAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var reader = new EdsReader();
        return reader.ReadStreamAsync(stream, cancellationToken);
    }

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing EDS content.</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadEdsAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var reader = new EdsReader();
        return reader.ReadStreamAsync(stream, maxInputSize, cancellationToken);
    }

    /// <summary>
    /// Writes an Electronic Data Sheet (EDS) to disk.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the EDS file should be written</param>
    /// <example>
    /// <code>
    /// var eds = CanOpenFile.ReadEds("template.eds");
    /// eds.FileInfo.FileRevision++;
    /// CanOpenFile.WriteEds(eds, "updated.eds");
    /// </code>
    /// </example>
    public static void WriteEds(ElectronicDataSheet eds, string filePath)
    {
        var writer = new EdsWriter();
        writer.WriteFile(eds, filePath);
    }

    /// <summary>
    /// Writes an Electronic Data Sheet (EDS) to a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="stream">Writable destination stream</param>
    public static void WriteEds(ElectronicDataSheet eds, Stream stream)
    {
        var writer = new EdsWriter();
        writer.WriteStream(eds, stream);
    }

    /// <summary>
    /// Writes an Electronic Data Sheet (EDS) to disk asynchronously.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the EDS file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    public static Task WriteEdsAsync(
        ElectronicDataSheet eds,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var writer = new EdsWriter();
        return writer.WriteFileAsync(eds, filePath, cancellationToken);
    }

    /// <summary>
    /// Writes an Electronic Data Sheet (EDS) to a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    public static Task WriteEdsAsync(
        ElectronicDataSheet eds,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var writer = new EdsWriter();
        return writer.WriteStreamAsync(eds, stream, cancellationToken);
    }

    /// <summary>
    /// Generates an EDS file content as string.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to convert</param>
    /// <returns>EDS content as string</returns>
    public static string WriteEdsToString(ElectronicDataSheet eds)
    {
        var writer = new EdsWriter();
        return writer.GenerateString(eds);
    }

    /// <summary>
    /// Reads a Device Configuration File (DCF).
    /// </summary>
    /// <param name="filePath">Path to the DCF file</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadDcf(string filePath)
    {
        return ReadDcf(filePath, IniParser.DefaultMaxInputSize);
    }

    /// <summary>
    /// Reads a Device Configuration File (DCF).
    /// </summary>
    /// <param name="filePath">Path to the DCF file</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    /// <example>
    /// <code>
    /// var dcf = CanOpenFile.ReadDcf("device_node2.dcf");
    /// Console.WriteLine($"Node ID: {dcf.DeviceCommissioning.NodeId}");
    /// Console.WriteLine($"Baudrate: {dcf.DeviceCommissioning.Baudrate} kbit/s");
    /// </code>
    /// </example>
    public static DeviceConfigurationFile ReadDcf(
        string filePath,
        long maxInputSize)
    {
        var reader = new DcfReader();
        return reader.ReadFile(filePath, maxInputSize);
    }

    /// <summary>
    /// Reads a Device Configuration File (DCF) asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the DCF file</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadDcfAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var reader = new DcfReader();
        return reader.ReadFileAsync(filePath, cancellationToken);
    }

    /// <summary>
    /// Reads a Device Configuration File (DCF) asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the DCF file</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadDcfAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var reader = new DcfReader();
        return reader.ReadFileAsync(filePath, maxInputSize, cancellationToken);
    }

    /// <summary>
    /// Reads a Device Configuration File (DCF) from a string.
    /// </summary>
    /// <param name="content">DCF file content as string</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadDcfFromString(string content)
    {
        return ReadDcfFromString(content, IniParser.DefaultMaxInputSize);
    }

    /// <summary>
    /// Reads a Device Configuration File (DCF) from a string.
    /// </summary>
    /// <param name="content">DCF file content as string</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadDcfFromString(
        string content,
        long maxInputSize)
    {
        var reader = new DcfReader();
        return reader.ReadString(content, maxInputSize);
    }

    /// <summary>
    /// Reads a Device Configuration File (DCF) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing DCF content.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadDcf(Stream stream)
    {
        return ReadDcf(stream, IniParser.DefaultMaxInputSize);
    }

    /// <summary>
    /// Reads a Device Configuration File (DCF) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing DCF content.</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadDcf(
        Stream stream,
        long maxInputSize)
    {
        var reader = new DcfReader();
        return reader.ReadStream(stream, maxInputSize);
    }

    /// <summary>
    /// Reads a Device Configuration File (DCF) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing DCF content.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadDcfAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var reader = new DcfReader();
        return reader.ReadStreamAsync(stream, cancellationToken);
    }

    /// <summary>
    /// Reads a Device Configuration File (DCF) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing DCF content.</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadDcfAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var reader = new DcfReader();
        return reader.ReadStreamAsync(stream, maxInputSize, cancellationToken);
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
    /// Writes a Device Configuration File (DCF) to a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="stream">Writable destination stream</param>
    public static void WriteDcf(DeviceConfigurationFile dcf, Stream stream)
    {
        var writer = new DcfWriter();
        writer.WriteStream(dcf, stream);
    }

    /// <summary>
    /// Writes a Device Configuration File (DCF) to disk asynchronously.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the DCF file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    public static Task WriteDcfAsync(
        DeviceConfigurationFile dcf,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var writer = new DcfWriter();
        return writer.WriteFileAsync(dcf, filePath, cancellationToken);
    }

    /// <summary>
    /// Writes a Device Configuration File (DCF) to a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    public static Task WriteDcfAsync(
        DeviceConfigurationFile dcf,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var writer = new DcfWriter();
        return writer.WriteStreamAsync(dcf, stream, cancellationToken);
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
        return ReadCpj(filePath, IniParser.DefaultMaxInputSize);
    }

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) file.
    /// </summary>
    /// <param name="filePath">Path to the CPJ file</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static NodelistProject ReadCpj(
        string filePath,
        long maxInputSize)
    {
        var reader = new CpjReader();
        return reader.ReadFile(filePath, maxInputSize);
    }

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the CPJ file</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static Task<NodelistProject> ReadCpjAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var reader = new CpjReader();
        return reader.ReadFileAsync(filePath, cancellationToken);
    }

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the CPJ file</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static Task<NodelistProject> ReadCpjAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var reader = new CpjReader();
        return reader.ReadFileAsync(filePath, maxInputSize, cancellationToken);
    }

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) from a string.
    /// </summary>
    /// <param name="content">CPJ file content as string</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static NodelistProject ReadCpjFromString(string content)
    {
        return ReadCpjFromString(content, IniParser.DefaultMaxInputSize);
    }

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) from a string.
    /// </summary>
    /// <param name="content">CPJ file content as string</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static NodelistProject ReadCpjFromString(
        string content,
        long maxInputSize)
    {
        var reader = new CpjReader();
        return reader.ReadString(content, maxInputSize);
    }

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing CPJ content.</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static NodelistProject ReadCpj(Stream stream)
    {
        return ReadCpj(stream, IniParser.DefaultMaxInputSize);
    }

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing CPJ content.</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static NodelistProject ReadCpj(
        Stream stream,
        long maxInputSize)
    {
        var reader = new CpjReader();
        return reader.ReadStream(stream, maxInputSize);
    }

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing CPJ content.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static Task<NodelistProject> ReadCpjAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var reader = new CpjReader();
        return reader.ReadStreamAsync(stream, cancellationToken);
    }

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing CPJ content.</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static Task<NodelistProject> ReadCpjAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var reader = new CpjReader();
        return reader.ReadStreamAsync(stream, maxInputSize, cancellationToken);
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
    /// Writes a CiA 306-3 nodelist project (.cpj) to a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="cpj">The NodelistProject to write</param>
    /// <param name="stream">Writable destination stream</param>
    public static void WriteCpj(NodelistProject cpj, Stream stream)
    {
        var writer = new CpjWriter();
        writer.WriteStream(cpj, stream);
    }

    /// <summary>
    /// Writes a CiA 306-3 nodelist project (.cpj) to disk asynchronously.
    /// </summary>
    /// <param name="cpj">The NodelistProject to write</param>
    /// <param name="filePath">Path where the CPJ file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    public static Task WriteCpjAsync(
        NodelistProject cpj,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var writer = new CpjWriter();
        return writer.WriteFileAsync(cpj, filePath, cancellationToken);
    }

    /// <summary>
    /// Writes a CiA 306-3 nodelist project (.cpj) to a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="cpj">The NodelistProject to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    public static Task WriteCpjAsync(
        NodelistProject cpj,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var writer = new CpjWriter();
        return writer.WriteStreamAsync(cpj, stream, cancellationToken);
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
    /// Reads a CiA 311 XDD (XML Device Description) file.
    /// </summary>
    /// <param name="filePath">Path to the XDD file</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadXdd(string filePath)
    {
        return ReadXdd(filePath, IniParser.DefaultMaxInputSize);
    }

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) file.
    /// </summary>
    /// <param name="filePath">Path to the XDD file</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadXdd(
        string filePath,
        long maxInputSize)
    {
        var reader = new XddReader();
        return reader.ReadFile(filePath, maxInputSize);
    }

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the XDD file</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadXddAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var reader = new XddReader();
        return reader.ReadFileAsync(filePath, cancellationToken);
    }

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the XDD file</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadXddAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var reader = new XddReader();
        return reader.ReadFileAsync(filePath, maxInputSize, cancellationToken);
    }

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) from a string.
    /// </summary>
    /// <param name="content">XDD file content as string</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadXddFromString(string content)
    {
        return ReadXddFromString(content, IniParser.DefaultMaxInputSize);
    }

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) from a string.
    /// </summary>
    /// <param name="content">XDD file content as string</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadXddFromString(
        string content,
        long maxInputSize)
    {
        var reader = new XddReader();
        return reader.ReadString(content, maxInputSize);
    }

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDD content.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadXdd(Stream stream)
    {
        return ReadXdd(stream, IniParser.DefaultMaxInputSize);
    }

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDD content.</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadXdd(
        Stream stream,
        long maxInputSize)
    {
        var reader = new XddReader();
        return reader.ReadStream(stream, maxInputSize);
    }

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDD content.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadXddAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var reader = new XddReader();
        return reader.ReadStreamAsync(stream, cancellationToken);
    }

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDD content.</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadXddAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var reader = new XddReader();
        return reader.ReadStreamAsync(stream, maxInputSize, cancellationToken);
    }

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) file.
    /// </summary>
    /// <param name="filePath">Path to the XDC file</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadXdc(string filePath)
    {
        return ReadXdc(filePath, IniParser.DefaultMaxInputSize);
    }

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) file.
    /// </summary>
    /// <param name="filePath">Path to the XDC file</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadXdc(
        string filePath,
        long maxInputSize)
    {
        var reader = new XdcReader();
        return reader.ReadFile(filePath, maxInputSize);
    }

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the XDC file</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadXdcAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var reader = new XdcReader();
        return reader.ReadFileAsync(filePath, cancellationToken);
    }

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the XDC file</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadXdcAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var reader = new XdcReader();
        return reader.ReadFileAsync(filePath, maxInputSize, cancellationToken);
    }

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) from a string.
    /// </summary>
    /// <param name="content">XDC file content as string</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadXdcFromString(string content)
    {
        return ReadXdcFromString(content, IniParser.DefaultMaxInputSize);
    }

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) from a string.
    /// </summary>
    /// <param name="content">XDC file content as string</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadXdcFromString(
        string content,
        long maxInputSize)
    {
        var reader = new XdcReader();
        return reader.ReadString(content, maxInputSize);
    }

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDC content.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadXdc(Stream stream)
    {
        return ReadXdc(stream, IniParser.DefaultMaxInputSize);
    }

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDC content.</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadXdc(
        Stream stream,
        long maxInputSize)
    {
        var reader = new XdcReader();
        return reader.ReadStream(stream, maxInputSize);
    }

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDC content.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadXdcAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var reader = new XdcReader();
        return reader.ReadStreamAsync(stream, cancellationToken);
    }

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDC content.</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters for this operation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadXdcAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var reader = new XdcReader();
        return reader.ReadStreamAsync(stream, maxInputSize, cancellationToken);
    }

    /// <summary>
    /// Writes an ElectronicDataSheet as a CiA 311 XDD file.
    /// </summary>
    /// <param name="xdd">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the XDD file should be written</param>
    public static void WriteXdd(ElectronicDataSheet xdd, string filePath)
    {
        var writer = new XddWriter();
        writer.WriteFile(xdd, filePath);
    }

    /// <summary>
    /// Writes an ElectronicDataSheet as a CiA 311 XDD stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="xdd">The ElectronicDataSheet to write</param>
    /// <param name="stream">Writable destination stream</param>
    public static void WriteXdd(ElectronicDataSheet xdd, Stream stream)
    {
        var writer = new XddWriter();
        writer.WriteStream(xdd, stream);
    }

    /// <summary>
    /// Writes an ElectronicDataSheet as a CiA 311 XDD file asynchronously.
    /// </summary>
    /// <param name="xdd">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the XDD file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    public static Task WriteXddAsync(
        ElectronicDataSheet xdd,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var writer = new XddWriter();
        return writer.WriteFileAsync(xdd, filePath, cancellationToken);
    }

    /// <summary>
    /// Writes an ElectronicDataSheet as a CiA 311 XDD stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="xdd">The ElectronicDataSheet to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    public static Task WriteXddAsync(
        ElectronicDataSheet xdd,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var writer = new XddWriter();
        return writer.WriteStreamAsync(xdd, stream, cancellationToken);
    }

    /// <summary>
    /// Generates XDD file content as string.
    /// </summary>
    /// <param name="xdd">The ElectronicDataSheet to convert</param>
    /// <returns>XDD content as string</returns>
    public static string WriteXddToString(ElectronicDataSheet xdd)
    {
        var writer = new XddWriter();
        return writer.GenerateString(xdd);
    }

    /// <summary>
    /// Writes a DeviceConfigurationFile as a CiA 311 XDC file.
    /// </summary>
    /// <param name="xdc">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the XDC file should be written</param>
    public static void WriteXdc(DeviceConfigurationFile xdc, string filePath)
    {
        var writer = new XdcWriter();
        writer.WriteFile(xdc, filePath);
    }

    /// <summary>
    /// Writes a DeviceConfigurationFile as a CiA 311 XDC stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="xdc">The DeviceConfigurationFile to write</param>
    /// <param name="stream">Writable destination stream</param>
    public static void WriteXdc(DeviceConfigurationFile xdc, Stream stream)
    {
        var writer = new XdcWriter();
        writer.WriteStream(xdc, stream);
    }

    /// <summary>
    /// Writes a DeviceConfigurationFile as a CiA 311 XDC file asynchronously.
    /// </summary>
    /// <param name="xdc">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the XDC file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    public static Task WriteXdcAsync(
        DeviceConfigurationFile xdc,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var writer = new XdcWriter();
        return writer.WriteFileAsync(xdc, filePath, cancellationToken);
    }

    /// <summary>
    /// Writes a DeviceConfigurationFile as a CiA 311 XDC stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="xdc">The DeviceConfigurationFile to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    public static Task WriteXdcAsync(
        DeviceConfigurationFile xdc,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var writer = new XdcWriter();
        return writer.WriteStreamAsync(xdc, stream, cancellationToken);
    }

    /// <summary>
    /// Generates XDC file content as string.
    /// </summary>
    /// <param name="xdc">The DeviceConfigurationFile to convert</param>
    /// <returns>XDC content as string</returns>
    public static string WriteXdcToString(DeviceConfigurationFile xdc)
    {
        var writer = new XdcWriter();
        return writer.GenerateString(xdc);
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
            DeviceInfo = ModelCloner.CloneDeviceInfo(eds.DeviceInfo),
            DeviceCommissioning = new DeviceCommissioning
            {
                NodeId = nodeId,
                Baudrate = baudrate,
                NodeName = nodeName ?? $"{eds.DeviceInfo.ProductName}_Node{nodeId}",
                NetNumber = 1,
                NetworkName = "CANopen Network",
                CANopenManager = false
            },
            ObjectDictionary = ModelCloner.CloneObjectDictionary(eds.ObjectDictionary),
            Comments = ModelCloner.CloneComments(eds.Comments),
            DynamicChannels = ModelCloner.CloneDynamicChannels(eds.DynamicChannels),
            // Preserve by reference (no deep clone yet) to avoid
            // partial/fragile cloning of the large CiA 311 object graph.
            ApplicationProcess = eds.ApplicationProcess
        };

        dcf.SupportedModules.AddRange(ModelCloner.CloneSupportedModules(eds.SupportedModules));
        dcf.Tools.AddRange(ModelCloner.CloneTools(eds.Tools));
        foreach (var kvp in ModelCloner.CloneAdditionalSections(eds.AdditionalSections))
            dcf.AdditionalSections[kvp.Key] = kvp.Value;

        return dcf;
    }
}
