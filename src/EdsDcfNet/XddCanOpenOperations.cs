namespace EdsDcfNet;

using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Writers;

/// <summary>
/// XDD-focused read/write operations for CiA 311 XML Device Descriptions.
/// Access via <see cref="CanOpenFile.Xdd"/>.
/// </summary>
public sealed class XddCanOpenOperations : FormatCanOpenOperations<ElectronicDataSheet>
{
    internal static XddCanOpenOperations Instance { get; } = new();

    private XddCanOpenOperations()
        : base(
            CanOpenWriteGuard.EnsureValidEdsForWrite,
            (filePath, maxInputSize) => new XddReader().ReadFile(filePath, maxInputSize),
            (filePath, maxInputSize, cancellationToken) =>
                new XddReader().ReadFileAsync(filePath, maxInputSize, cancellationToken),
            (content, maxInputSize) => new XddReader().ReadString(content, maxInputSize),
            (stream, maxInputSize) => new XddReader().ReadStream(stream, maxInputSize),
            (stream, maxInputSize, cancellationToken) =>
                new XddReader().ReadStreamAsync(stream, maxInputSize, cancellationToken),
            (xdd, filePath) => new XddWriter().WriteFile(xdd, filePath),
            (xdd, stream) => new XddWriter().WriteStream(xdd, stream),
            (xdd, filePath, cancellationToken) =>
                new XddWriter().WriteFileAsync(xdd, filePath, cancellationToken),
            (xdd, stream, cancellationToken) =>
                new XddWriter().WriteStreamAsync(xdd, stream, cancellationToken),
            xdd => new XddWriter().GenerateString(xdd))
    {
    }
}
