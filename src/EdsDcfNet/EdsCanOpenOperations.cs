namespace EdsDcfNet;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Utilities;
using EdsDcfNet.Validation;
using EdsDcfNet.Writers;
using System.Globalization;

/// <summary>
/// EDS-focused read/write operations for CiA DS 306 Electronic Data Sheets.
/// Access via <see cref="CanOpenFile.Eds"/>.
/// </summary>
#pragma warning disable CA1822 // Instance API exposed via CanOpenFile.Eds entry point.
public sealed class EdsCanOpenOperations : FormatCanOpenOperations<ElectronicDataSheet>
{
    internal static EdsCanOpenOperations Instance { get; } = new();

    private EdsCanOpenOperations()
        : base(
            (model, options) => CanOpenWriteGuard.EnsureValidForWrite(model, options, IniWriteRules.Apply),
            (filePath, maxInputSize) => new EdsReader().ReadFile(filePath, maxInputSize),
            (filePath, maxInputSize, cancellationToken) =>
                new EdsReader().ReadFileAsync(filePath, maxInputSize, cancellationToken),
            (content, maxInputSize) => new EdsReader().ReadString(content, maxInputSize),
            (stream, maxInputSize) => new EdsReader().ReadStream(stream, maxInputSize),
            (stream, maxInputSize, cancellationToken) =>
                new EdsReader().ReadStreamAsync(stream, maxInputSize, cancellationToken),
            (eds, filePath) => new EdsWriter().WriteFile(eds, filePath),
            (eds, stream) => new EdsWriter().WriteStream(eds, stream),
            (eds, filePath, cancellationToken) =>
                new EdsWriter().WriteFileAsync(eds, filePath, cancellationToken),
            (eds, stream, cancellationToken) =>
                new EdsWriter().WriteStreamAsync(eds, stream, cancellationToken),
            eds => new EdsWriter().GenerateString(eds),
            (model, options, cancellationToken) => CanOpenWriteGuard.EnsureValidForWriteAsync(
                model,
                options,
                IniWriteRules.Apply,
                cancellationToken))
    {
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
    /// conversion is applied. The generated <c>Description</c> names the source EDS file; when the
    /// EDS has no <c>FileName</c> it is <c>DCF generated from EDS</c>.
    /// </remarks>
    /// <returns>A new DeviceConfigurationFile</returns>
    public DeviceConfigurationFile ConvertToDcf(
        ElectronicDataSheet eds,
        byte nodeId,
        DateTime timestamp,
        ushort baudrate = 250,
        string? nodeName = null)
    {
        if (!CanOpenNodeId.IsInRange(nodeId))
            throw new ArgumentOutOfRangeException(
                nameof(nodeId),
                nodeId,
                "CANopen Node-ID must be in range " + CanOpenNodeId.RangeDescription + ".");

        var dcf = new DeviceConfigurationFile
        {
            FileInfo = new EdsFileInfo
            {
                FileName = Path.ChangeExtension(eds.FileInfo.FileName, ".dcf"),
                FileVersion = eds.FileInfo.FileVersion,
                FileRevision = (byte)Math.Min(eds.FileInfo.FileRevision + 1, byte.MaxValue),
                EdsVersion = eds.FileInfo.EdsVersion,
                Description = string.IsNullOrWhiteSpace(eds.FileInfo.FileName)
                    ? "DCF generated from EDS"
                    : $"DCF generated from {eds.FileInfo.FileName.Trim()}",
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
                NodeName = nodeName ?? eds.DeviceInfo.ProductName + "_Node" + nodeId.ToString(CultureInfo.InvariantCulture),
                NetNumber = 1,
                NetworkName = "CANopen Network",
                CANopenManager = false
            },
            ObjectDictionary = ModelCloner.CloneObjectDictionary(eds.ObjectDictionary),
            Comments = ModelCloner.CloneComments(eds.Comments),
            DynamicChannels = ModelCloner.CloneDynamicChannels(eds.DynamicChannels),
            ApplicationProcess = ModelCloner.CloneApplicationProcess(eds.ApplicationProcess),
            XddPreserved = ModelCloner.CloneXddPreserved(eds.XddPreserved)
        };

        dcf.SupportedModules.AddRange(ModelCloner.CloneSupportedModules(eds.SupportedModules));
        dcf.Tools.AddRange(ModelCloner.CloneTools(eds.Tools));
        foreach (var kvp in ModelCloner.CloneAdditionalSections(eds.AdditionalSections))
            dcf.AdditionalSections[kvp.Key] = kvp.Value;
        ModelCloner.CopyAdditionalSectionOrder(eds, dcf);
        foreach (var kvp in ModelCloner.CloneSectionRemainingEntries(eds.SectionRemainingEntries))
            dcf.SectionRemainingEntries[kvp.Key] = kvp.Value;

        // Unmapped [FileInfo] entries travel with the file; the DCF writer skips LastEDS,
        // which the conversion sets from the source file name.
        ModelCloner.CopyFileInfoRemainingEntries(eds.FileInfo, dcf.FileInfo);
        ModelCloner.CopyFileVersionText(eds.FileInfo, dcf.FileInfo);

        // Without a source file name there is no derived LastEDS. A kept, non-empty LastEDS
        // entry of the EDS then becomes the property. An empty one stays a kept entry, which
        // the DCF writer outputs while the property is empty.
        if (string.IsNullOrEmpty(dcf.FileInfo.LastEds)
            && dcf.FileInfo.RemainingEntries.TryGetValue("LastEDS", out var retainedLastEds)
            && !string.IsNullOrEmpty(retainedLastEds))
        {
            dcf.FileInfo.LastEds = retainedLastEds;
            dcf.FileInfo.RemainingEntries.Remove("LastEDS");
        }

        // EDS stores DCF-only keywords in RemainingEntries. Move them onto the
        // properties so a commissioned value assigned after conversion is the
        // only copy written.
        foreach (var obj in dcf.ObjectDictionary.Objects.Values)
            ModelCloner.AdoptDcfKeywords(obj);

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

    /// <summary>
    /// Writes an EDS to disk.
    /// </summary>
    public override void WriteFile(ElectronicDataSheet eds, string filePath)
        => base.WriteFile(eds, filePath);

    /// <summary>
    /// Writes an EDS to disk.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public override void WriteFile(ElectronicDataSheet eds, string filePath, CanOpenWriteOptions? options)
        => base.WriteFile(eds, filePath, options);

    /// <summary>
    /// Writes an EDS to a stream. The stream is not disposed.
    /// </summary>
    public override void WriteStream(ElectronicDataSheet eds, Stream stream)
        => base.WriteStream(eds, stream);

    /// <summary>
    /// Writes an EDS to a stream. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public override void WriteStream(ElectronicDataSheet eds, Stream stream, CanOpenWriteOptions? options)
        => base.WriteStream(eds, stream, options);

    /// <summary>
    /// Writes an EDS to disk asynchronously.
    /// </summary>
    public override Task WriteFileAsync(
        ElectronicDataSheet eds,
        string filePath,
        CancellationToken cancellationToken = default)
        => base.WriteFileAsync(eds, filePath, cancellationToken);

    /// <summary>
    /// Writes an EDS to disk asynchronously.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public override Task WriteFileAsync(
        ElectronicDataSheet eds,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => base.WriteFileAsync(eds, filePath, options, cancellationToken);

    /// <summary>
    /// Writes an EDS to a stream asynchronously. The stream is not disposed.
    /// </summary>
    public override Task WriteStreamAsync(
        ElectronicDataSheet eds,
        Stream stream,
        CancellationToken cancellationToken = default)
        => base.WriteStreamAsync(eds, stream, cancellationToken);

    /// <summary>
    /// Writes an EDS to a stream asynchronously. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public override Task WriteStreamAsync(
        ElectronicDataSheet eds,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => base.WriteStreamAsync(eds, stream, options, cancellationToken);

    /// <summary>
    /// Serializes an EDS to a string.
    /// </summary>
    public override string WriteToString(ElectronicDataSheet eds)
        => base.WriteToString(eds);

    /// <summary>
    /// Serializes an EDS to a string.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public override string WriteToString(ElectronicDataSheet eds, CanOpenWriteOptions? options)
        => base.WriteToString(eds, options);
}
#pragma warning restore CA1822
