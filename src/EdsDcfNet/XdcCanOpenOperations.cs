namespace EdsDcfNet;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Writers;

/// <summary>
/// XDC-focused read/write operations for CiA 311 XML Device Configurations.
/// Access via <see cref="CanOpenFile.Xdc"/>.
/// </summary>
#pragma warning disable CA1822 // Instance API exposed via CanOpenFile.Xdc entry point.
public sealed class XdcCanOpenOperations
{
    internal static XdcCanOpenOperations Instance { get; } = new();

    private XdcCanOpenOperations()
    {
    }

    /// <summary>
    /// Reads an XDC file from disk.
    /// </summary>
    public DeviceConfigurationFile ReadFile(string filePath, CanOpenFileOptions? options = null)
    {
        var reader = new XdcReader();
        return reader.ReadFile(filePath, CanOpenFileOptions.ResolveMaxInputSize(options));
    }

    /// <summary>
    /// Reads an XDC file from disk asynchronously.
    /// </summary>
    public Task<DeviceConfigurationFile> ReadFileAsync(
        string filePath,
        CanOpenFileOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var reader = new XdcReader();
        return reader.ReadFileAsync(filePath, CanOpenFileOptions.ResolveMaxInputSize(options), cancellationToken);
    }

    /// <summary>
    /// Reads an XDC from a string.
    /// </summary>
    public DeviceConfigurationFile ReadString(string content, CanOpenFileOptions? options = null)
    {
        var reader = new XdcReader();
        return reader.ReadString(content, CanOpenFileOptions.ResolveMaxInputSize(options));
    }

    /// <summary>
    /// Reads an XDC from a stream. The stream is not disposed.
    /// </summary>
    public DeviceConfigurationFile ReadStream(Stream stream, CanOpenFileOptions? options = null)
    {
        var reader = new XdcReader();
        return reader.ReadStream(stream, CanOpenFileOptions.ResolveMaxInputSize(options));
    }

    /// <summary>
    /// Reads an XDC from a stream asynchronously. The stream is not disposed.
    /// </summary>
    public Task<DeviceConfigurationFile> ReadStreamAsync(
        Stream stream,
        CanOpenFileOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var reader = new XdcReader();
        return reader.ReadStreamAsync(stream, CanOpenFileOptions.ResolveMaxInputSize(options), cancellationToken);
    }

    /// <summary>
    /// Writes an XDC to disk.
    /// </summary>
    public void WriteFile(DeviceConfigurationFile xdc, string filePath)
        => WriteFile(xdc, filePath, options: null);

    /// <summary>
    /// Writes an XDC to disk.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public void WriteFile(DeviceConfigurationFile xdc, string filePath, CanOpenWriteOptions? options)
    {
        CanOpenWriteGuard.EnsureValidForWrite(xdc, options);
        var writer = new XdcWriter();
        writer.WriteFile(xdc, filePath);
    }

    /// <summary>
    /// Writes an XDC to a stream. The stream is not disposed.
    /// </summary>
    public void WriteStream(DeviceConfigurationFile xdc, Stream stream)
        => WriteStream(xdc, stream, options: null);

    /// <summary>
    /// Writes an XDC to a stream. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public void WriteStream(DeviceConfigurationFile xdc, Stream stream, CanOpenWriteOptions? options)
    {
        CanOpenWriteGuard.EnsureValidForWrite(xdc, options);
        var writer = new XdcWriter();
        writer.WriteStream(xdc, stream);
    }

    /// <summary>
    /// Writes an XDC to disk asynchronously.
    /// </summary>
    public Task WriteFileAsync(
        DeviceConfigurationFile xdc,
        string filePath,
        CancellationToken cancellationToken = default)
        => WriteFileAsync(xdc, filePath, options: null, cancellationToken);

    /// <summary>
    /// Writes an XDC to disk asynchronously.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public Task WriteFileAsync(
        DeviceConfigurationFile xdc,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        CanOpenWriteGuard.EnsureValidForWrite(xdc, options);
        var writer = new XdcWriter();
        return writer.WriteFileAsync(xdc, filePath, cancellationToken);
    }

    /// <summary>
    /// Writes an XDC to a stream asynchronously. The stream is not disposed.
    /// </summary>
    public Task WriteStreamAsync(
        DeviceConfigurationFile xdc,
        Stream stream,
        CancellationToken cancellationToken = default)
        => WriteStreamAsync(xdc, stream, options: null, cancellationToken);

    /// <summary>
    /// Writes an XDC to a stream asynchronously. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public Task WriteStreamAsync(
        DeviceConfigurationFile xdc,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        CanOpenWriteGuard.EnsureValidForWrite(xdc, options);
        var writer = new XdcWriter();
        return writer.WriteStreamAsync(xdc, stream, cancellationToken);
    }

    /// <summary>
    /// Serializes an XDC to a string.
    /// </summary>
    public string WriteToString(DeviceConfigurationFile xdc)
        => WriteToString(xdc, options: null);

    /// <summary>
    /// Serializes an XDC to a string.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public string WriteToString(DeviceConfigurationFile xdc, CanOpenWriteOptions? options)
    {
        CanOpenWriteGuard.EnsureValidForWrite(xdc, options);
        var writer = new XdcWriter();
        return writer.GenerateString(xdc);
    }
}
#pragma warning restore CA1822
