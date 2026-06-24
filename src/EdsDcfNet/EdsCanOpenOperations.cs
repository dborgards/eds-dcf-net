namespace EdsDcfNet;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Writers;

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
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public void WriteFile(ElectronicDataSheet eds, string filePath, CanOpenWriteOptions? options = null)
    {
        CanOpenWriteGuard.EnsureValidEdsForWrite(eds, options);
        var writer = new EdsWriter();
        writer.WriteFile(eds, filePath);
    }

    /// <summary>
    /// Writes an EDS to a stream. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public void WriteStream(ElectronicDataSheet eds, Stream stream, CanOpenWriteOptions? options = null)
    {
        CanOpenWriteGuard.EnsureValidEdsForWrite(eds, options);
        var writer = new EdsWriter();
        writer.WriteStream(eds, stream);
    }

    /// <summary>
    /// Writes an EDS to disk asynchronously.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public Task WriteFileAsync(
        ElectronicDataSheet eds,
        string filePath,
        CanOpenWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        CanOpenWriteGuard.EnsureValidEdsForWrite(eds, options);
        var writer = new EdsWriter();
        return writer.WriteFileAsync(eds, filePath, cancellationToken);
    }

    /// <summary>
    /// Writes an EDS to a stream asynchronously. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public Task WriteStreamAsync(
        ElectronicDataSheet eds,
        Stream stream,
        CanOpenWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        CanOpenWriteGuard.EnsureValidEdsForWrite(eds, options);
        var writer = new EdsWriter();
        return writer.WriteStreamAsync(eds, stream, cancellationToken);
    }

    /// <summary>
    /// Serializes an EDS to a string.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public string WriteToString(ElectronicDataSheet eds, CanOpenWriteOptions? options = null)
    {
        CanOpenWriteGuard.EnsureValidEdsForWrite(eds, options);
        var writer = new EdsWriter();
        return writer.GenerateString(eds);
    }
}
#pragma warning restore CA1822
