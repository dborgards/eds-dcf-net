namespace EdsDcfNet;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Writers;

/// <summary>
/// XDC-focused read/write operations for CiA 311 XML Device Configurations.
/// Access via <see cref="CanOpenFile.Xdc"/>.
/// </summary>
public sealed class XdcCanOpenOperations : FormatCanOpenOperations<DeviceConfigurationFile>
{
    internal static XdcCanOpenOperations Instance { get; } = new();

    private XdcCanOpenOperations()
        : base(
            CanOpenWriteGuard.EnsureValidForWrite,
            (filePath, maxInputSize) => new XdcReader().ReadFile(filePath, maxInputSize),
            (filePath, maxInputSize, cancellationToken) =>
                new XdcReader().ReadFileAsync(filePath, maxInputSize, cancellationToken),
            (content, maxInputSize) => new XdcReader().ReadString(content, maxInputSize),
            (stream, maxInputSize) => new XdcReader().ReadStream(stream, maxInputSize),
            (stream, maxInputSize, cancellationToken) =>
                new XdcReader().ReadStreamAsync(stream, maxInputSize, cancellationToken),
            (xdc, filePath) => new XdcWriter().WriteFile(xdc, filePath),
            (xdc, stream) => new XdcWriter().WriteStream(xdc, stream),
            (xdc, filePath, cancellationToken) =>
                new XdcWriter().WriteFileAsync(xdc, filePath, cancellationToken),
            (xdc, stream, cancellationToken) =>
                new XdcWriter().WriteStreamAsync(xdc, stream, cancellationToken),
            xdc => new XdcWriter().GenerateString(xdc))
    {
    }

    /// <summary>
    /// Writes an XDC to disk.
    /// </summary>
    public new void WriteFile(DeviceConfigurationFile xdc, string filePath)
        => base.WriteFile(xdc, filePath);

    /// <summary>
    /// Writes an XDC to disk.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public new void WriteFile(DeviceConfigurationFile xdc, string filePath, CanOpenWriteOptions? options)
        => base.WriteFile(xdc, filePath, options);

    /// <summary>
    /// Writes an XDC to a stream. The stream is not disposed.
    /// </summary>
    public new void WriteStream(DeviceConfigurationFile xdc, Stream stream)
        => base.WriteStream(xdc, stream);

    /// <summary>
    /// Writes an XDC to a stream. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public new void WriteStream(DeviceConfigurationFile xdc, Stream stream, CanOpenWriteOptions? options)
        => base.WriteStream(xdc, stream, options);

    /// <summary>
    /// Writes an XDC to disk asynchronously.
    /// </summary>
    public new Task WriteFileAsync(
        DeviceConfigurationFile xdc,
        string filePath,
        CancellationToken cancellationToken = default)
        => base.WriteFileAsync(xdc, filePath, cancellationToken);

    /// <summary>
    /// Writes an XDC to disk asynchronously.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public new Task WriteFileAsync(
        DeviceConfigurationFile xdc,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => base.WriteFileAsync(xdc, filePath, options, cancellationToken);

    /// <summary>
    /// Writes an XDC to a stream asynchronously. The stream is not disposed.
    /// </summary>
    public new Task WriteStreamAsync(
        DeviceConfigurationFile xdc,
        Stream stream,
        CancellationToken cancellationToken = default)
        => base.WriteStreamAsync(xdc, stream, cancellationToken);

    /// <summary>
    /// Writes an XDC to a stream asynchronously. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public new Task WriteStreamAsync(
        DeviceConfigurationFile xdc,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => base.WriteStreamAsync(xdc, stream, options, cancellationToken);

    /// <summary>
    /// Serializes an XDC to a string.
    /// </summary>
    public new string WriteToString(DeviceConfigurationFile xdc)
        => base.WriteToString(xdc);

    /// <summary>
    /// Serializes an XDC to a string.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public new string WriteToString(DeviceConfigurationFile xdc, CanOpenWriteOptions? options)
        => base.WriteToString(xdc, options);
}
