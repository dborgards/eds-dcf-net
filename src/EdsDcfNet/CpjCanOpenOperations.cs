namespace EdsDcfNet;

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
}
