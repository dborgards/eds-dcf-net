namespace EdsDcfNet;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Writers;

/// <summary>
/// CPJ-focused read/write operations for CiA DS 306-3 nodelist projects.
/// Access via <see cref="CanOpenFile.Cpj"/>.
/// </summary>
public sealed class CpjCanOpenOperations : FormatCanOpenOperations<NodelistProject>
{
    internal static CpjCanOpenOperations Instance { get; } = new();

    private CpjCanOpenOperations()
        : base(
            CanOpenWriteGuard.EnsureValidCpjForWrite,
            (filePath, maxInputSize) => new CpjReader().ReadFile(filePath, maxInputSize),
            (filePath, maxInputSize, cancellationToken) =>
                new CpjReader().ReadFileAsync(filePath, maxInputSize, cancellationToken),
            (content, maxInputSize) => new CpjReader().ReadString(content, maxInputSize),
            (stream, maxInputSize) => new CpjReader().ReadStream(stream, maxInputSize),
            (stream, maxInputSize, cancellationToken) =>
                new CpjReader().ReadStreamAsync(stream, maxInputSize, cancellationToken),
            (cpj, filePath) => new CpjWriter().WriteFile(cpj, filePath),
            (cpj, stream) => new CpjWriter().WriteStream(cpj, stream),
            (cpj, filePath, cancellationToken) =>
                new CpjWriter().WriteFileAsync(cpj, filePath, cancellationToken),
            (cpj, stream, cancellationToken) =>
                new CpjWriter().WriteStreamAsync(cpj, stream, cancellationToken),
            cpj => new CpjWriter().GenerateString(cpj))
    {
    }

    /// <summary>
    /// Writes a CPJ to disk.
    /// </summary>
    public new void WriteFile(NodelistProject cpj, string filePath)
        => base.WriteFile(cpj, filePath);

    /// <summary>
    /// Writes a CPJ to disk.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public new void WriteFile(NodelistProject cpj, string filePath, CanOpenWriteOptions? options)
        => base.WriteFile(cpj, filePath, options);

    /// <summary>
    /// Writes a CPJ to a stream. The stream is not disposed.
    /// </summary>
    public new void WriteStream(NodelistProject cpj, Stream stream)
        => base.WriteStream(cpj, stream);

    /// <summary>
    /// Writes a CPJ to a stream. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public new void WriteStream(NodelistProject cpj, Stream stream, CanOpenWriteOptions? options)
        => base.WriteStream(cpj, stream, options);

    /// <summary>
    /// Writes a CPJ to disk asynchronously.
    /// </summary>
    public new Task WriteFileAsync(
        NodelistProject cpj,
        string filePath,
        CancellationToken cancellationToken = default)
        => base.WriteFileAsync(cpj, filePath, cancellationToken);

    /// <summary>
    /// Writes a CPJ to disk asynchronously.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public new Task WriteFileAsync(
        NodelistProject cpj,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => base.WriteFileAsync(cpj, filePath, options, cancellationToken);

    /// <summary>
    /// Writes a CPJ to a stream asynchronously. The stream is not disposed.
    /// </summary>
    public new Task WriteStreamAsync(
        NodelistProject cpj,
        Stream stream,
        CancellationToken cancellationToken = default)
        => base.WriteStreamAsync(cpj, stream, cancellationToken);

    /// <summary>
    /// Writes a CPJ to a stream asynchronously. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public new Task WriteStreamAsync(
        NodelistProject cpj,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => base.WriteStreamAsync(cpj, stream, options, cancellationToken);

    /// <summary>
    /// Serializes a CPJ to a string.
    /// </summary>
    public new string WriteToString(NodelistProject cpj)
        => base.WriteToString(cpj);

    /// <summary>
    /// Serializes a CPJ to a string.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public new string WriteToString(NodelistProject cpj, CanOpenWriteOptions? options)
        => base.WriteToString(cpj, options);
}
