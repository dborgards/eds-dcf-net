namespace EdsDcfNet;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Writers;

/// <summary>
/// CPJ-focused read/write operations for CiA DS 306-3 nodelist projects.
/// Access via <see cref="CanOpenFile.Cpj"/>.
/// </summary>
#pragma warning disable CA1822 // Instance API exposed via CanOpenFile.Cpj entry point.
public sealed class CpjCanOpenOperations
{
    internal static CpjCanOpenOperations Instance { get; } = new();

    private CpjCanOpenOperations()
    {
    }

    /// <summary>
    /// Reads a CPJ file from disk.
    /// </summary>
    public NodelistProject ReadFile(string filePath, CanOpenFileOptions? options = null)
    {
        var reader = new CpjReader();
        return reader.ReadFile(filePath, CanOpenFileOptions.ResolveMaxInputSize(options));
    }

    /// <summary>
    /// Reads a CPJ file from disk asynchronously.
    /// </summary>
    public Task<NodelistProject> ReadFileAsync(
        string filePath,
        CanOpenFileOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var reader = new CpjReader();
        return reader.ReadFileAsync(filePath, CanOpenFileOptions.ResolveMaxInputSize(options), cancellationToken);
    }

    /// <summary>
    /// Reads a CPJ from a string.
    /// </summary>
    public NodelistProject ReadString(string content, CanOpenFileOptions? options = null)
    {
        var reader = new CpjReader();
        return reader.ReadString(content, CanOpenFileOptions.ResolveMaxInputSize(options));
    }

    /// <summary>
    /// Reads a CPJ from a stream. The stream is not disposed.
    /// </summary>
    public NodelistProject ReadStream(Stream stream, CanOpenFileOptions? options = null)
    {
        var reader = new CpjReader();
        return reader.ReadStream(stream, CanOpenFileOptions.ResolveMaxInputSize(options));
    }

    /// <summary>
    /// Reads a CPJ from a stream asynchronously. The stream is not disposed.
    /// </summary>
    public Task<NodelistProject> ReadStreamAsync(
        Stream stream,
        CanOpenFileOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var reader = new CpjReader();
        return reader.ReadStreamAsync(stream, CanOpenFileOptions.ResolveMaxInputSize(options), cancellationToken);
    }

    /// <summary>
    /// Writes a CPJ to disk.
    /// </summary>
    public void WriteFile(NodelistProject cpj, string filePath)
        => WriteFile(cpj, filePath, options: null);

    /// <summary>
    /// Writes a CPJ to disk.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public void WriteFile(NodelistProject cpj, string filePath, CanOpenWriteOptions? options)
    {
        CanOpenWriteGuard.EnsureValidCpjForWrite(cpj, options);
        var writer = new CpjWriter();
        writer.WriteFile(cpj, filePath);
    }

    /// <summary>
    /// Writes a CPJ to a stream. The stream is not disposed.
    /// </summary>
    public void WriteStream(NodelistProject cpj, Stream stream)
        => WriteStream(cpj, stream, options: null);

    /// <summary>
    /// Writes a CPJ to a stream. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public void WriteStream(NodelistProject cpj, Stream stream, CanOpenWriteOptions? options)
    {
        CanOpenWriteGuard.EnsureValidCpjForWrite(cpj, options);
        var writer = new CpjWriter();
        writer.WriteStream(cpj, stream);
    }

    /// <summary>
    /// Writes a CPJ to disk asynchronously.
    /// </summary>
    public Task WriteFileAsync(
        NodelistProject cpj,
        string filePath,
        CancellationToken cancellationToken = default)
        => WriteFileAsync(cpj, filePath, options: null, cancellationToken);

    /// <summary>
    /// Writes a CPJ to disk asynchronously.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public Task WriteFileAsync(
        NodelistProject cpj,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        CanOpenWriteGuard.EnsureValidCpjForWrite(cpj, options);
        var writer = new CpjWriter();
        return writer.WriteFileAsync(cpj, filePath, cancellationToken);
    }

    /// <summary>
    /// Writes a CPJ to a stream asynchronously. The stream is not disposed.
    /// </summary>
    public Task WriteStreamAsync(
        NodelistProject cpj,
        Stream stream,
        CancellationToken cancellationToken = default)
        => WriteStreamAsync(cpj, stream, options: null, cancellationToken);

    /// <summary>
    /// Writes a CPJ to a stream asynchronously. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public Task WriteStreamAsync(
        NodelistProject cpj,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        CanOpenWriteGuard.EnsureValidCpjForWrite(cpj, options);
        var writer = new CpjWriter();
        return writer.WriteStreamAsync(cpj, stream, cancellationToken);
    }

    /// <summary>
    /// Serializes a CPJ to a string.
    /// </summary>
    public string WriteToString(NodelistProject cpj)
        => WriteToString(cpj, options: null);

    /// <summary>
    /// Serializes a CPJ to a string.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public string WriteToString(NodelistProject cpj, CanOpenWriteOptions? options)
    {
        CanOpenWriteGuard.EnsureValidCpjForWrite(cpj, options);
        var writer = new CpjWriter();
        return writer.GenerateString(cpj);
    }
}
#pragma warning restore CA1822
