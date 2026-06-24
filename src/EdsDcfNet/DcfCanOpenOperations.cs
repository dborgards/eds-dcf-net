namespace EdsDcfNet;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Writers;

/// <summary>
/// DCF-focused read/write operations for CiA DS 306 Device Configuration Files.
/// Access via <see cref="CanOpenFile.Dcf"/>.
/// </summary>
#pragma warning disable CA1822 // Instance API exposed via CanOpenFile.Dcf entry point.
public sealed class DcfCanOpenOperations
{
    internal static DcfCanOpenOperations Instance { get; } = new();

    private DcfCanOpenOperations()
    {
    }

    /// <summary>
    /// Reads a DCF file from disk.
    /// </summary>
    public DeviceConfigurationFile ReadFile(string filePath, CanOpenFileOptions? options = null)
    {
        var reader = new DcfReader();
        return reader.ReadFile(filePath, CanOpenFileOptions.ResolveMaxInputSize(options));
    }

    /// <summary>
    /// Reads a DCF file from disk asynchronously.
    /// </summary>
    public Task<DeviceConfigurationFile> ReadFileAsync(
        string filePath,
        CanOpenFileOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var reader = new DcfReader();
        return reader.ReadFileAsync(filePath, CanOpenFileOptions.ResolveMaxInputSize(options), cancellationToken);
    }

    /// <summary>
    /// Reads a DCF from a string.
    /// </summary>
    public DeviceConfigurationFile ReadString(string content, CanOpenFileOptions? options = null)
    {
        var reader = new DcfReader();
        return reader.ReadString(content, CanOpenFileOptions.ResolveMaxInputSize(options));
    }

    /// <summary>
    /// Reads a DCF from a stream. The stream is not disposed.
    /// </summary>
    public DeviceConfigurationFile ReadStream(Stream stream, CanOpenFileOptions? options = null)
    {
        var reader = new DcfReader();
        return reader.ReadStream(stream, CanOpenFileOptions.ResolveMaxInputSize(options));
    }

    /// <summary>
    /// Reads a DCF from a stream asynchronously. The stream is not disposed.
    /// </summary>
    public Task<DeviceConfigurationFile> ReadStreamAsync(
        Stream stream,
        CanOpenFileOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var reader = new DcfReader();
        return reader.ReadStreamAsync(stream, CanOpenFileOptions.ResolveMaxInputSize(options), cancellationToken);
    }

    /// <summary>
    /// Writes a DCF to disk.
    /// </summary>
    public void WriteFile(DeviceConfigurationFile dcf, string filePath)
        => WriteFile(dcf, filePath, options: null);

    /// <summary>
    /// Writes a DCF to disk.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public void WriteFile(DeviceConfigurationFile dcf, string filePath, CanOpenWriteOptions? options)
    {
        CanOpenWriteGuard.EnsureValidDcfForWrite(dcf, options);
        var writer = new DcfWriter();
        writer.WriteFile(dcf, filePath);
    }

    /// <summary>
    /// Writes a DCF to a stream. The stream is not disposed.
    /// </summary>
    public void WriteStream(DeviceConfigurationFile dcf, Stream stream)
        => WriteStream(dcf, stream, options: null);

    /// <summary>
    /// Writes a DCF to a stream. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public void WriteStream(DeviceConfigurationFile dcf, Stream stream, CanOpenWriteOptions? options)
    {
        CanOpenWriteGuard.EnsureValidDcfForWrite(dcf, options);
        var writer = new DcfWriter();
        writer.WriteStream(dcf, stream);
    }

    /// <summary>
    /// Writes a DCF to disk asynchronously.
    /// </summary>
    public Task WriteFileAsync(
        DeviceConfigurationFile dcf,
        string filePath,
        CancellationToken cancellationToken = default)
        => WriteFileAsync(dcf, filePath, options: null, cancellationToken);

    /// <summary>
    /// Writes a DCF to disk asynchronously.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public Task WriteFileAsync(
        DeviceConfigurationFile dcf,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        CanOpenWriteGuard.EnsureValidDcfForWrite(dcf, options);
        var writer = new DcfWriter();
        return writer.WriteFileAsync(dcf, filePath, cancellationToken);
    }

    /// <summary>
    /// Writes a DCF to a stream asynchronously. The stream is not disposed.
    /// </summary>
    public Task WriteStreamAsync(
        DeviceConfigurationFile dcf,
        Stream stream,
        CancellationToken cancellationToken = default)
        => WriteStreamAsync(dcf, stream, options: null, cancellationToken);

    /// <summary>
    /// Writes a DCF to a stream asynchronously. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public Task WriteStreamAsync(
        DeviceConfigurationFile dcf,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        CanOpenWriteGuard.EnsureValidDcfForWrite(dcf, options);
        var writer = new DcfWriter();
        return writer.WriteStreamAsync(dcf, stream, cancellationToken);
    }

    /// <summary>
    /// Serializes a DCF to a string.
    /// </summary>
    public string WriteToString(DeviceConfigurationFile dcf)
        => WriteToString(dcf, options: null);

    /// <summary>
    /// Serializes a DCF to a string.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public string WriteToString(DeviceConfigurationFile dcf, CanOpenWriteOptions? options)
    {
        CanOpenWriteGuard.EnsureValidDcfForWrite(dcf, options);
        var writer = new DcfWriter();
        return writer.GenerateString(dcf);
    }
}
#pragma warning restore CA1822
