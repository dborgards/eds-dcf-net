namespace EdsDcfNet;

using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Writers;

/// <summary>
/// DCF-focused read/write operations for CiA DS 306 Device Configuration Files.
/// Access via <see cref="CanOpenFile.Dcf"/>.
/// </summary>
public sealed class DcfCanOpenOperations : FormatCanOpenOperations<DeviceConfigurationFile>
{
    internal static DcfCanOpenOperations Instance { get; } = new();

    private DcfCanOpenOperations()
        : base(
            CanOpenWriteGuard.EnsureValidDcfForWrite,
            (filePath, maxInputSize) => new DcfReader().ReadFile(filePath, maxInputSize),
            (filePath, maxInputSize, cancellationToken) =>
                new DcfReader().ReadFileAsync(filePath, maxInputSize, cancellationToken),
            (content, maxInputSize) => new DcfReader().ReadString(content, maxInputSize),
            (stream, maxInputSize) => new DcfReader().ReadStream(stream, maxInputSize),
            (stream, maxInputSize, cancellationToken) =>
                new DcfReader().ReadStreamAsync(stream, maxInputSize, cancellationToken),
            (dcf, filePath) => new DcfWriter().WriteFile(dcf, filePath),
            (dcf, stream) => new DcfWriter().WriteStream(dcf, stream),
            (dcf, filePath, cancellationToken) =>
                new DcfWriter().WriteFileAsync(dcf, filePath, cancellationToken),
            (dcf, stream, cancellationToken) =>
                new DcfWriter().WriteStreamAsync(dcf, stream, cancellationToken),
            dcf => new DcfWriter().GenerateString(dcf))
    {
    }
}
