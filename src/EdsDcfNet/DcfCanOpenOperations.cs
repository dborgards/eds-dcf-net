namespace EdsDcfNet;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Writers;

/// <summary>
/// DCF-focused read/write operations for CiA DS 306 Device Configuration Files.
/// Access via <see cref="CanOpenFile.Dcf"/>.
/// </summary>
public sealed class DcfCanOpenOperations : FormatCanOpenOperations<DeviceConfigurationFile>
{
    internal static DcfCanOpenOperations Instance { get; } = new();

    private DcfCanOpenOperations()
        : base(
            CanOpenWriteGuard.EnsureValidForWrite,
            (filePath, maxInputSize) => new DcfReader().ReadFile(filePath, maxInputSize),
            (filePath, maxInputSize, cancellationToken) =>
                new DcfReader().ReadFileAsync(filePath, maxInputSize, cancellationToken),
            (content, maxInputSize) => new DcfReader().ReadString(content, maxInputSize),
            (stream, maxInputSize) => new DcfReader().ReadStream(stream, maxInputSize),
            (stream, maxInputSize, cancellationToken) =>
                new DcfReader().ReadStreamAsync(stream, maxInputSize, cancellationToken),
            (dcf, filePath) => new DcfWriter().WriteFile(dcf, filePath),
            (dcf, stream) => new DcfWriter().WriteStream(dcf, stream),
            (dcf, filePath, cancellationToken) =>
                new DcfWriter().WriteFileAsync(dcf, filePath, cancellationToken),
            (dcf, stream, cancellationToken) =>
                new DcfWriter().WriteStreamAsync(dcf, stream, cancellationToken),
            dcf => new DcfWriter().GenerateString(dcf))
    {
    }

    /// <summary>
    /// Writes a DCF to disk.
    /// </summary>
    public override void WriteFile(DeviceConfigurationFile dcf, string filePath)
        => base.WriteFile(dcf, filePath);

    /// <summary>
    /// Writes a DCF to disk.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public override void WriteFile(DeviceConfigurationFile dcf, string filePath, CanOpenWriteOptions? options)
        => base.WriteFile(dcf, filePath, options);

    /// <summary>
    /// Writes a DCF to a stream. The stream is not disposed.
    /// </summary>
    public override void WriteStream(DeviceConfigurationFile dcf, Stream stream)
        => base.WriteStream(dcf, stream);

    /// <summary>
    /// Writes a DCF to a stream. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public override void WriteStream(DeviceConfigurationFile dcf, Stream stream, CanOpenWriteOptions? options)
        => base.WriteStream(dcf, stream, options);

    /// <summary>
    /// Writes a DCF to disk asynchronously.
    /// </summary>
    public override Task WriteFileAsync(
        DeviceConfigurationFile dcf,
        string filePath,
        CancellationToken cancellationToken = default)
        => base.WriteFileAsync(dcf, filePath, cancellationToken);

    /// <summary>
    /// Writes a DCF to disk asynchronously.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public override Task WriteFileAsync(
        DeviceConfigurationFile dcf,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => base.WriteFileAsync(dcf, filePath, options, cancellationToken);

    /// <summary>
    /// Writes a DCF to a stream asynchronously. The stream is not disposed.
    /// </summary>
    public override Task WriteStreamAsync(
        DeviceConfigurationFile dcf,
        Stream stream,
        CancellationToken cancellationToken = default)
        => base.WriteStreamAsync(dcf, stream, cancellationToken);

    /// <summary>
    /// Writes a DCF to a stream asynchronously. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public override Task WriteStreamAsync(
        DeviceConfigurationFile dcf,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => base.WriteStreamAsync(dcf, stream, options, cancellationToken);

    /// <summary>
    /// Serializes a DCF to a string.
    /// </summary>
    public override string WriteToString(DeviceConfigurationFile dcf)
        => base.WriteToString(dcf);

    /// <summary>
    /// Serializes a DCF to a string.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public override string WriteToString(DeviceConfigurationFile dcf, CanOpenWriteOptions? options)
        => base.WriteToString(dcf, options);
}
