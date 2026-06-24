namespace EdsDcfNet;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Utilities;
using EdsDcfNet.Writers;
using System.Globalization;

/// <summary>
/// EDS-focused read/write operations for CiA DS 306 Electronic Data Sheets.
/// Access via <see cref="CanOpenFile.Eds"/>.
/// </summary>
#pragma warning disable CA1822 // Instance API exposed via CanOpenFile.Eds entry point.
public sealed class EdsCanOpenOperations
{
    internal static EdsCanOpenOperations Instance { get; } = new();

    private EdsCanOpenOperations()
    {
    }

    /// <summary>
    /// Reads an EDS file from disk.
    /// </summary>
    public ElectronicDataSheet ReadFile(string filePath, CanOpenFileOptions? options = null)
    {
        var reader = new EdsReader();
        return reader.ReadFile(filePath, CanOpenFileOptions.ResolveMaxInputSize(options));
    }

    /// <summary>
    /// Reads an EDS file from disk asynchronously.
    /// </summary>
    public Task<ElectronicDataSheet> ReadFileAsync(
        string filePath,
        CanOpenFileOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var reader = new EdsReader();
        return reader.ReadFileAsync(filePath, CanOpenFileOptions.ResolveMaxInputSize(options), cancellationToken);
    }

    /// <summary>
    /// Reads an EDS from a string.
    /// </summary>
    public ElectronicDataSheet ReadString(string content, CanOpenFileOptions? options = null)
    {
        var reader = new EdsReader();
        return reader.ReadString(content, CanOpenFileOptions.ResolveMaxInputSize(options));
    }

    /// <summary>
    /// Reads an EDS from a stream. The stream is not disposed.
    /// </summary>
    public ElectronicDataSheet ReadStream(Stream stream, CanOpenFileOptions? options = null)
    {
        var reader = new EdsReader();
        return reader.ReadStream(stream, CanOpenFileOptions.ResolveMaxInputSize(options));
    }

    /// <summary>
    /// Reads an EDS from a stream asynchronously. The stream is not disposed.
    /// </summary>
    public Task<ElectronicDataSheet> ReadStreamAsync(
        Stream stream,
        CanOpenFileOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var reader = new EdsReader();
        return reader.ReadStreamAsync(stream, CanOpenFileOptions.ResolveMaxInputSize(options), cancellationToken);
    }

    /// <summary>
    /// Writes an EDS to disk.
    /// </summary>
    public void WriteFile(ElectronicDataSheet eds, string filePath)
        => WriteFile(eds, filePath, options: null);

    /// <summary>
    /// Writes an EDS to disk.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public void WriteFile(ElectronicDataSheet eds, string filePath, CanOpenWriteOptions? options)
    {
        CanOpenWriteGuard.EnsureValidEdsForWrite(eds, options);
        var writer = new EdsWriter();
        writer.WriteFile(eds, filePath);
    }

    /// <summary>
    /// Writes an EDS to a stream. The stream is not disposed.
    /// </summary>
    public void WriteStream(ElectronicDataSheet eds, Stream stream)
        => WriteStream(eds, stream, options: null);

    /// <summary>
    /// Writes an EDS to a stream. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public void WriteStream(ElectronicDataSheet eds, Stream stream, CanOpenWriteOptions? options)
    {
        CanOpenWriteGuard.EnsureValidEdsForWrite(eds, options);
        var writer = new EdsWriter();
        writer.WriteStream(eds, stream);
    }

    /// <summary>
    /// Writes an EDS to disk asynchronously.
    /// </summary>
    public Task WriteFileAsync(
        ElectronicDataSheet eds,
        string filePath,
        CancellationToken cancellationToken = default)
        => WriteFileAsync(eds, filePath, options: null, cancellationToken);

    /// <summary>
    /// Writes an EDS to disk asynchronously.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public Task WriteFileAsync(
        ElectronicDataSheet eds,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        CanOpenWriteGuard.EnsureValidEdsForWrite(eds, options);
        var writer = new EdsWriter();
        return writer.WriteFileAsync(eds, filePath, cancellationToken);
    }

    /// <summary>
    /// Writes an EDS to a stream asynchronously. The stream is not disposed.
    /// </summary>
    public Task WriteStreamAsync(
        ElectronicDataSheet eds,
        Stream stream,
        CancellationToken cancellationToken = default)
        => WriteStreamAsync(eds, stream, options: null, cancellationToken);

    /// <summary>
    /// Writes an EDS to a stream asynchronously. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public Task WriteStreamAsync(
        ElectronicDataSheet eds,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        CanOpenWriteGuard.EnsureValidEdsForWrite(eds, options);
        var writer = new EdsWriter();
        return writer.WriteStreamAsync(eds, stream, cancellationToken);
    }

    /// <summary>
    /// Serializes an EDS to a string.
    /// </summary>
    public string WriteToString(ElectronicDataSheet eds)
        => WriteToString(eds, options: null);

    /// <summary>
    /// Serializes an EDS to a string.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public string WriteToString(ElectronicDataSheet eds, CanOpenWriteOptions? options)
    {
        CanOpenWriteGuard.EnsureValidEdsForWrite(eds, options);
        var writer = new EdsWriter();
        return writer.GenerateString(eds);
    }

    /// <summary>
    /// Converts an EDS to a DCF with specified commissioning parameters and an explicit timestamp.
    /// </summary>
    /// <param name="eds">The EDS to convert</param>
    /// <param name="nodeId">Node ID for the device</param>
    /// <param name="timestamp">Timestamp used for generated FileInfo creation date/time fields.</param>
    /// <param name="baudrate">Baudrate in kbit/s (default: 250)</param>
    /// <param name="nodeName">Optional node name</param>
    /// <remarks>
    /// The timestamp is formatted with invariant culture as <c>MM-dd-yyyy</c> for
    /// <c>CreationDate</c> and <c>hh:mmtt</c> for <c>CreationTime</c>; no timezone
    /// conversion is applied.
    /// </remarks>
    /// <returns>A new DeviceConfigurationFile</returns>
    public DeviceConfigurationFile ConvertToDcf(
        ElectronicDataSheet eds,
        byte nodeId,
        DateTime timestamp,
        ushort baudrate = 250,
        string? nodeName = null)
    {
        if (nodeId < 1 || nodeId > 127)
            throw new ArgumentOutOfRangeException(nameof(nodeId), nodeId, "CANopen Node-ID must be in range 1..127.");

        var dcf = new DeviceConfigurationFile
        {
            FileInfo = new EdsFileInfo
            {
                FileName = Path.ChangeExtension(eds.FileInfo.FileName, ".dcf"),
                FileVersion = eds.FileInfo.FileVersion,
                FileRevision = (byte)Math.Min(eds.FileInfo.FileRevision + 1, byte.MaxValue),
                EdsVersion = eds.FileInfo.EdsVersion,
                Description = $"DCF generated from {eds.FileInfo.FileName}",
                CreationDate = timestamp.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture),
                CreationTime = timestamp.ToString("hh:mmtt", CultureInfo.InvariantCulture),
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
            ApplicationProcess = ModelCloner.CloneApplicationProcess(eds.ApplicationProcess)
        };

        dcf.SupportedModules.AddRange(ModelCloner.CloneSupportedModules(eds.SupportedModules));
        dcf.Tools.AddRange(ModelCloner.CloneTools(eds.Tools));
        foreach (var kvp in ModelCloner.CloneAdditionalSections(eds.AdditionalSections))
            dcf.AdditionalSections[kvp.Key] = kvp.Value;

        return dcf;
    }

    /// <summary>
    /// Converts an EDS to a DCF with specified commissioning parameters.
    /// </summary>
    /// <param name="eds">The EDS to convert</param>
    /// <param name="nodeId">Node ID for the device</param>
    /// <param name="baudrate">Baudrate in kbit/s (default: 250)</param>
    /// <param name="nodeName">Optional node name</param>
    /// <remarks>
    /// Uses <see cref="DateTime.UtcNow"/> for generated FileInfo date/time fields.
    /// Use <see cref="ConvertToDcf(ElectronicDataSheet, byte, DateTime, ushort, string?)"/> for fully deterministic output.
    /// </remarks>
    /// <returns>A new DeviceConfigurationFile</returns>
    public DeviceConfigurationFile ConvertToDcf(
        ElectronicDataSheet eds,
        byte nodeId,
        ushort baudrate = 250,
        string? nodeName = null)
        => ConvertToDcf(eds, nodeId, DateTime.UtcNow, baudrate, nodeName);
}
#pragma warning restore CA1822
