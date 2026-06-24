namespace EdsDcfNet;

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
            CanOpenWriteGuard.EnsureValidDcfForWrite,
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
}
