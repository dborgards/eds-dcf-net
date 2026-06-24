namespace EdsDcfNet;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Writers;

/// <summary>
/// XDD-focused read/write operations for CiA 311 XML Device Descriptions.
/// Access via <see cref="CanOpenFile.Xdd"/>.
/// </summary>
#pragma warning disable CA1822 // Instance API exposed via CanOpenFile.Xdd entry point.
public sealed class XddCanOpenOperations
{
    internal static XddCanOpenOperations Instance { get; } = new();

    private XddCanOpenOperations()
    {
    }

    /// <summary>
    /// Reads an XDD file from disk.
    /// </summary>
    public ElectronicDataSheet ReadFile(string filePath, CanOpenFileOptions? options = null)
    {
        var reader = new XddReader();
        return reader.ReadFile(filePath, CanOpenFileOptions.ResolveMaxInputSize(options));
    }

    /// <summary>
    /// Reads an XDD file from disk asynchronously.
    /// </summary>
    public Task<ElectronicDataSheet> ReadFileAsync(
        string filePath,
        CanOpenFileOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var reader = new XddReader();
        return reader.ReadFileAsync(filePath, CanOpenFileOptions.ResolveMaxInputSize(options), cancellationToken);
    }

    /// <summary>
    /// Reads an XDD from a string.
    /// </summary>
    public ElectronicDataSheet ReadString(string content, CanOpenFileOptions? options = null)
    {
        var reader = new XddReader();
        return reader.ReadString(content, CanOpenFileOptions.ResolveMaxInputSize(options));
    }

    /// <summary>
    /// Reads an XDD from a stream. The stream is not disposed.
    /// </summary>
    public ElectronicDataSheet ReadStream(Stream stream, CanOpenFileOptions? options = null)
    {
        var reader = new XddReader();
        return reader.ReadStream(stream, CanOpenFileOptions.ResolveMaxInputSize(options));
    }

    /// <summary>
    /// Reads an XDD from a stream asynchronously. The stream is not disposed.
    /// </summary>
    public Task<ElectronicDataSheet> ReadStreamAsync(
        Stream stream,
        CanOpenFileOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var reader = new XddReader();
        return reader.ReadStreamAsync(stream, CanOpenFileOptions.ResolveMaxInputSize(options), cancellationToken);
    }

    /// <summary>
    /// Writes an XDD to disk.
    /// </summary>
    public void WriteFile(ElectronicDataSheet xdd, string filePath)
        => WriteFile(xdd, filePath, options: null);

    /// <summary>
    /// Writes an XDD to disk.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public void WriteFile(ElectronicDataSheet xdd, string filePath, CanOpenWriteOptions? options)
    {
        CanOpenWriteGuard.EnsureValidForWrite(xdd, options);
        var writer = new XddWriter();
        writer.WriteFile(xdd, filePath);
    }

    /// <summary>
    /// Writes an XDD to a stream. The stream is not disposed.
    /// </summary>
    public void WriteStream(ElectronicDataSheet xdd, Stream stream)
        => WriteStream(xdd, stream, options: null);

    /// <summary>
    /// Writes an XDD to a stream. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public void WriteStream(ElectronicDataSheet xdd, Stream stream, CanOpenWriteOptions? options)
    {
        CanOpenWriteGuard.EnsureValidForWrite(xdd, options);
        var writer = new XddWriter();
        writer.WriteStream(xdd, stream);
    }

    /// <summary>
    /// Writes an XDD to disk asynchronously.
    /// </summary>
    public Task WriteFileAsync(
        ElectronicDataSheet xdd,
        string filePath,
        CancellationToken cancellationToken = default)
        => WriteFileAsync(xdd, filePath, options: null, cancellationToken);

    /// <summary>
    /// Writes an XDD to disk asynchronously.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public Task WriteFileAsync(
        ElectronicDataSheet xdd,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        CanOpenWriteGuard.EnsureValidForWrite(xdd, options);
        var writer = new XddWriter();
        return writer.WriteFileAsync(xdd, filePath, cancellationToken);
    }

    /// <summary>
    /// Writes an XDD to a stream asynchronously. The stream is not disposed.
    /// </summary>
    public Task WriteStreamAsync(
        ElectronicDataSheet xdd,
        Stream stream,
        CancellationToken cancellationToken = default)
        => WriteStreamAsync(xdd, stream, options: null, cancellationToken);

    /// <summary>
    /// Writes an XDD to a stream asynchronously. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public Task WriteStreamAsync(
        ElectronicDataSheet xdd,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        CanOpenWriteGuard.EnsureValidForWrite(xdd, options);
        var writer = new XddWriter();
        return writer.WriteStreamAsync(xdd, stream, cancellationToken);
    }

    /// <summary>
    /// Serializes an XDD to a string.
    /// </summary>
    public string WriteToString(ElectronicDataSheet xdd)
        => WriteToString(xdd, options: null);

    /// <summary>
    /// Serializes an XDD to a string.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public string WriteToString(ElectronicDataSheet xdd, CanOpenWriteOptions? options)
    {
        CanOpenWriteGuard.EnsureValidForWrite(xdd, options);
        var writer = new XddWriter();
        return writer.GenerateString(xdd);
    }
}
#pragma warning restore CA1822
