namespace EdsDcfNet;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Validation;
using EdsDcfNet.Writers;

/// <summary>
/// XDD-focused read/write operations for CiA 311 XML Device Descriptions.
/// Access via <see cref="CanOpenFile.Xdd"/>.
/// </summary>
/// <remarks>
/// The default write is tolerant. <c>fileCreationDate</c> (required by the schema) is written only
/// for a valid <see cref="EdsFileInfo.CreationDate"/>, so a model without one produces a document that
/// is not schema-valid, as does an empty object dictionary. A date or time that is not valid is omitted.
/// With <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> these cases throw
/// <see cref="ModelValidationException"/>. <see cref="EdsFileInfo.CreationTime"/> and
/// <see cref="EdsFileInfo.ModificationTime"/> are written as <c>xsd:time</c>: an <c>xsd:time</c> value
/// unchanged, the EDS form <c>hh:mmAM/PM</c> converted to <c>HH:mm:ss</c>.
/// </remarks>
public sealed class XddCanOpenOperations : FormatCanOpenOperations<ElectronicDataSheet>
{
    internal static XddCanOpenOperations Instance { get; } = new();

    private XddCanOpenOperations()
        : base(
            (model, options) => CanOpenWriteGuard.EnsureValidForWrite(model, options, XmlWriteRules.Apply),
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
            xdd => new XddWriter().GenerateString(xdd),
            (model, options, cancellationToken) => CanOpenWriteGuard.EnsureValidForWriteAsync(
                model,
                options,
                XmlWriteRules.Apply,
                cancellationToken))
    {
    }

    /// <summary>
    /// Writes an XDD to disk.
    /// </summary>
    public override void WriteFile(ElectronicDataSheet xdd, string filePath)
        => base.WriteFile(xdd, filePath);

    /// <summary>
    /// Writes an XDD to disk.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public override void WriteFile(ElectronicDataSheet xdd, string filePath, CanOpenWriteOptions? options)
        => base.WriteFile(xdd, filePath, options);

    /// <summary>
    /// Writes an XDD to a stream. The stream is not disposed.
    /// </summary>
    public override void WriteStream(ElectronicDataSheet xdd, Stream stream)
        => base.WriteStream(xdd, stream);

    /// <summary>
    /// Writes an XDD to a stream. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public override void WriteStream(ElectronicDataSheet xdd, Stream stream, CanOpenWriteOptions? options)
        => base.WriteStream(xdd, stream, options);

    /// <summary>
    /// Writes an XDD to disk asynchronously.
    /// </summary>
    public override Task WriteFileAsync(
        ElectronicDataSheet xdd,
        string filePath,
        CancellationToken cancellationToken = default)
        => base.WriteFileAsync(xdd, filePath, cancellationToken);

    /// <summary>
    /// Writes an XDD to disk asynchronously.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public override Task WriteFileAsync(
        ElectronicDataSheet xdd,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => base.WriteFileAsync(xdd, filePath, options, cancellationToken);

    /// <summary>
    /// Writes an XDD to a stream asynchronously. The stream is not disposed.
    /// </summary>
    public override Task WriteStreamAsync(
        ElectronicDataSheet xdd,
        Stream stream,
        CancellationToken cancellationToken = default)
        => base.WriteStreamAsync(xdd, stream, cancellationToken);

    /// <summary>
    /// Writes an XDD to a stream asynchronously. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public override Task WriteStreamAsync(
        ElectronicDataSheet xdd,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => base.WriteStreamAsync(xdd, stream, options, cancellationToken);

    /// <summary>
    /// Serializes an XDD to a string.
    /// </summary>
    public override string WriteToString(ElectronicDataSheet xdd)
        => base.WriteToString(xdd);

    /// <summary>
    /// Serializes an XDD to a string.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public override string WriteToString(ElectronicDataSheet xdd, CanOpenWriteOptions? options)
        => base.WriteToString(xdd, options);
}
