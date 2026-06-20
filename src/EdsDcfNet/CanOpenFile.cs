namespace EdsDcfNet;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Utilities;
using EdsDcfNet.Validation;
using EdsDcfNet.Writers;
using System.Globalization;

/// <summary>
/// Main entry point for working with EDS and DCF files.
/// Provides a simple, fluent API for reading and writing CANopen configuration files.
/// </summary>
/// <remarks>
/// File and stream writer overloads of <c>WriteEds</c>, <c>WriteDcf</c>, <c>WriteCpj</c>,
/// <c>WriteXdd</c>, and <c>WriteXdc</c> serialize text as UTF-8 without BOM.
/// This intentionally diverges from strict historical ASCII-only assumptions in DS 306 to
/// preserve non-ASCII content while remaining ASCII-compatible for 7-bit data.
/// The corresponding <c>Write*ToString</c> overloads return a .NET <see cref="string"/>,
/// so BOM and byte-level encoding do not apply.
/// For format-specific operations with shared options, prefer <see cref="Eds"/>,
/// <see cref="Dcf"/>, <see cref="Cpj"/>, <see cref="Xdd"/>, and <see cref="Xdc"/>.
/// </remarks>
public static class CanOpenFile
{
    /// <summary>
    /// EDS read/write operations. Prefer this entry point for new code that needs
    /// <see cref="CanOpenFileOptions"/> instead of additional <see cref="CanOpenFile"/> overloads.
    /// </summary>
    public static EdsCanOpenOperations Eds { get; } = EdsCanOpenOperations.Instance;
    /// <summary>
    /// DCF read/write operations.
    /// </summary>
    public static DcfCanOpenOperations Dcf { get; } = DcfCanOpenOperations.Instance;

    /// <summary>
    /// CPJ read/write operations.
    /// </summary>
    public static CpjCanOpenOperations Cpj { get; } = CpjCanOpenOperations.Instance;

    /// <summary>
    /// XDD read/write operations.
    /// </summary>
    public static XddCanOpenOperations Xdd { get; } = XddCanOpenOperations.Instance;

    /// <summary>
    /// XDC read/write operations.
    /// </summary>
    public static XdcCanOpenOperations Xdc { get; } = XdcCanOpenOperations.Instance;


    /// <summary>
    /// Validates an Electronic Data Sheet (EDS) model using the full
    /// <see cref="CanOpenModelValidator"/> rule set.
    /// </summary>
    /// <param name="eds">Model instance to validate</param>
    /// <returns>List of validation issues. Empty when model is valid.</returns>
    public static IReadOnlyList<ValidationIssue> Validate(ElectronicDataSheet eds)
    {
        return CanOpenModelValidator.Validate(eds);
    }

    /// <summary>
    /// Validates a Device Configuration File (DCF) model using the full
    /// <see cref="CanOpenModelValidator"/> rule set.
    /// </summary>
    /// <param name="dcf">Model instance to validate</param>
    /// <remarks>
    /// For commissioned device entries, <c>NodeId</c> must be in range <c>1..127</c>.
    /// <c>NodeId == 0</c> is accepted only when commissioning is omitted
    /// (all commissioning fields are left at their default or empty values).
    /// <c>Baudrate == 0</c> is accepted for this omitted commissioning state.
    /// </remarks>
    /// <returns>List of validation issues. Empty when model is valid.</returns>
    public static IReadOnlyList<ValidationIssue> Validate(DeviceConfigurationFile dcf)
    {
        return CanOpenModelValidator.Validate(dcf);
    }

    /// <summary>
    /// Validates a nodelist project (CPJ) model using the full
    /// <see cref="CanOpenModelValidator"/> rule set.
    /// </summary>
    /// <param name="cpj">Model instance to validate</param>
    /// <returns>List of validation issues. Empty when model is valid.</returns>
    public static IReadOnlyList<ValidationIssue> Validate(NodelistProject cpj)
    {
        return CanOpenModelValidator.Validate(cpj);
    }

    /// <summary>
    /// Validates an EDS model and throws <see cref="ModelValidationException"/> when issues are found.
    /// </summary>
    /// <param name="eds">Model instance to validate</param>
    /// <exception cref="ModelValidationException">Thrown when validation issues are found.</exception>
    public static void EnsureValid(ElectronicDataSheet eds)
    {
        ThrowIfInvalid(Validate(eds));
    }

    /// <summary>
    /// Validates a DCF model and throws <see cref="ModelValidationException"/> when issues are found.
    /// </summary>
    /// <param name="dcf">Model instance to validate</param>
    /// <exception cref="ModelValidationException">Thrown when validation issues are found.</exception>
    public static void EnsureValid(DeviceConfigurationFile dcf)
    {
        ThrowIfInvalid(Validate(dcf));
    }

    /// <summary>
    /// Validates a CPJ model and throws <see cref="ModelValidationException"/> when issues are found.
    /// </summary>
    /// <param name="cpj">Model instance to validate</param>
    /// <exception cref="ModelValidationException">Thrown when validation issues are found.</exception>
    public static void EnsureValid(NodelistProject cpj)
    {
        ThrowIfInvalid(Validate(cpj));
    }

    #region EDS Read

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) file.
    /// </summary>
    /// <param name="filePath">Path to the EDS file</param>
    /// <param name="maxInputSize">Maximum file size in bytes when reading from <paramref name="filePath"/>.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    /// <example>
    /// <code>
    /// var eds = CanOpenFile.ReadEds("device.eds");
    /// Console.WriteLine($"Device: {eds.DeviceInfo.ProductName}");
    /// </code>
    /// </example>
    public static ElectronicDataSheet ReadEds(
        string filePath,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Eds.ReadFile(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the EDS file</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadEdsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => Eds.ReadFileAsync(filePath, cancellationToken: cancellationToken);

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the EDS file</param>
    /// <param name="maxInputSize">Maximum file size in bytes when reading from <paramref name="filePath"/>.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadEdsAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Eds.ReadFileAsync(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) from a string.
    /// </summary>
    /// <param name="content">EDS file content as string</param>
    /// <param name="maxInputSize">Maximum content length in decoded characters.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadEdsFromString(
        string content,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Eds.ReadString(content, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing EDS content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters read from <paramref name="stream"/>.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadEds(
        Stream stream,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Eds.ReadStream(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing EDS content.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadEdsAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => Eds.ReadStreamAsync(stream, cancellationToken: cancellationToken);

    /// <summary>
    /// Reads an Electronic Data Sheet (EDS) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing EDS content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters read from <paramref name="stream"/>.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadEdsAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Eds.ReadStreamAsync(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    #endregion

    #region EDS Write

    /// <summary>
    /// Writes an Electronic Data Sheet (EDS) to disk.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the EDS file should be written</param>
    /// <example>
    /// <code>
    /// var eds = CanOpenFile.ReadEds("template.eds");
    /// eds.FileInfo.FileRevision++;
    /// CanOpenFile.WriteEds(eds, "updated.eds");
    /// </code>
    /// </example>
    public static void WriteEds(ElectronicDataSheet eds, string filePath)
        => WriteEds(eds, filePath, options: null);

    /// <summary>
    /// Writes an Electronic Data Sheet (EDS) to disk.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the EDS file should be written</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static void WriteEds(ElectronicDataSheet eds, string filePath, CanOpenWriteOptions? options)
    {
        EnsureValidEdsForWrite(eds, options);
        Eds.WriteFile(eds, filePath);
    }

    /// <summary>
    /// Writes an Electronic Data Sheet (EDS) to a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="stream">Writable destination stream</param>
    public static void WriteEds(ElectronicDataSheet eds, Stream stream)
        => WriteEds(eds, stream, options: null);

    /// <summary>
    /// Writes an Electronic Data Sheet (EDS) to a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static void WriteEds(ElectronicDataSheet eds, Stream stream, CanOpenWriteOptions? options)
    {
        EnsureValidEdsForWrite(eds, options);
        Eds.WriteStream(eds, stream);
    }

    /// <summary>
    /// Writes an Electronic Data Sheet (EDS) to disk asynchronously.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the EDS file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    public static Task WriteEdsAsync(
        ElectronicDataSheet eds,
        string filePath,
        CancellationToken cancellationToken = default)
        => WriteEdsAsync(eds, filePath, options: null, cancellationToken);

    /// <summary>
    /// Writes an Electronic Data Sheet (EDS) to disk asynchronously.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the EDS file should be written</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static Task WriteEdsAsync(
        ElectronicDataSheet eds,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        EnsureValidEdsForWrite(eds, options);
        return Eds.WriteFileAsync(eds, filePath, cancellationToken);
    }

    /// <summary>
    /// Writes an Electronic Data Sheet (EDS) to a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    public static Task WriteEdsAsync(
        ElectronicDataSheet eds,
        Stream stream,
        CancellationToken cancellationToken = default)
        => WriteEdsAsync(eds, stream, options: null, cancellationToken);

    /// <summary>
    /// Writes an Electronic Data Sheet (EDS) to a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static Task WriteEdsAsync(
        ElectronicDataSheet eds,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        EnsureValidEdsForWrite(eds, options);
        return Eds.WriteStreamAsync(eds, stream, cancellationToken);
    }

    /// <summary>
    /// Generates an EDS file content as string.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to convert</param>
    /// <returns>EDS content as string</returns>
    public static string WriteEdsToString(ElectronicDataSheet eds)
        => WriteEdsToString(eds, options: null);

    /// <summary>
    /// Generates an EDS file content as string.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to convert</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <returns>EDS content as string</returns>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static string WriteEdsToString(ElectronicDataSheet eds, CanOpenWriteOptions? options)
    {
        EnsureValidEdsForWrite(eds, options);
        return Eds.WriteToString(eds);
    }

    #endregion

    #region DCF Read

    /// <summary>
    /// Reads a Device Configuration File (DCF).
    /// </summary>
    /// <param name="filePath">Path to the DCF file</param>
    /// <param name="maxInputSize">Maximum file size in bytes when reading from <paramref name="filePath"/>.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    /// <example>
    /// <code>
    /// var dcf = CanOpenFile.ReadDcf("device_node2.dcf");
    /// Console.WriteLine($"Node ID: {dcf.DeviceCommissioning.NodeId}");
    /// Console.WriteLine($"Baudrate: {dcf.DeviceCommissioning.Baudrate} kbit/s");
    /// </code>
    /// </example>
    public static DeviceConfigurationFile ReadDcf(
        string filePath,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Dcf.ReadFile(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <summary>
    /// Reads a Device Configuration File (DCF).
    /// </summary>
    /// <param name="filePath">Path to the DCF file</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadDcf(string filePath, CanOpenFileOptions options)
        => Dcf.ReadFile(filePath, options);

    /// <summary>
    /// Reads a Device Configuration File (DCF) asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the DCF file</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadDcfAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => Dcf.ReadFileAsync(filePath, cancellationToken: cancellationToken);

    /// <summary>
    /// Reads a Device Configuration File (DCF) asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the DCF file</param>
    /// <param name="maxInputSize">Maximum file size in bytes when reading from <paramref name="filePath"/>.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadDcfAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Dcf.ReadFileAsync(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <summary>
    /// Reads a Device Configuration File (DCF) asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the DCF file</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadDcfAsync(
        string filePath,
        CanOpenFileOptions options,
        CancellationToken cancellationToken = default)
        => Dcf.ReadFileAsync(filePath, options, cancellationToken);

    /// <summary>
    /// Reads a Device Configuration File (DCF) from a string.
    /// </summary>
    /// <param name="content">DCF file content as string</param>
    /// <param name="maxInputSize">Maximum content length in decoded characters.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadDcfFromString(
        string content,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Dcf.ReadString(content, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <summary>
    /// Reads a Device Configuration File (DCF) from a string.
    /// </summary>
    /// <param name="content">DCF file content as string</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadDcfFromString(string content, CanOpenFileOptions options)
        => Dcf.ReadString(content, options);

    /// <summary>
    /// Reads a Device Configuration File (DCF) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing DCF content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters read from <paramref name="stream"/>.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadDcf(
        Stream stream,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Dcf.ReadStream(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <summary>
    /// Reads a Device Configuration File (DCF) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing DCF content.</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadDcf(Stream stream, CanOpenFileOptions options)
        => Dcf.ReadStream(stream, options);

    /// <summary>
    /// Reads a Device Configuration File (DCF) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing DCF content.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadDcfAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => Dcf.ReadStreamAsync(stream, cancellationToken: cancellationToken);

    /// <summary>
    /// Reads a Device Configuration File (DCF) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing DCF content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters read from <paramref name="stream"/>.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadDcfAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Dcf.ReadStreamAsync(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <summary>
    /// Reads a Device Configuration File (DCF) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing DCF content.</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadDcfAsync(
        Stream stream,
        CanOpenFileOptions options,
        CancellationToken cancellationToken = default)
        => Dcf.ReadStreamAsync(stream, options, cancellationToken);

    #endregion


    #region DCF Write

    /// <summary>
    /// Writes a Device Configuration File (DCF) to disk.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the DCF file should be written</param>
    /// <example>
    /// <code>
    /// var dcf = CanOpenFile.ReadDcf("template.dcf");
    /// dcf.DeviceCommissioning.NodeId = 5;
    /// dcf.DeviceCommissioning.Baudrate = 500;
    /// CanOpenFile.WriteDcf(dcf, "configured_device.dcf");
    /// </code>
    /// </example>
    public static void WriteDcf(DeviceConfigurationFile dcf, string filePath)
        => WriteDcf(dcf, filePath, options: null);

    /// <summary>
    /// Writes a Device Configuration File (DCF) to disk.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the DCF file should be written</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static void WriteDcf(DeviceConfigurationFile dcf, string filePath, CanOpenWriteOptions? options)
    {
        EnsureValidDcfForWrite(dcf, options);
        var writer = new DcfWriter();
        writer.WriteFile(dcf, filePath);
    }

    /// <summary>
    /// Writes a Device Configuration File (DCF) to a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="stream">Writable destination stream</param>
    public static void WriteDcf(DeviceConfigurationFile dcf, Stream stream)
        => WriteDcf(dcf, stream, options: null);

    /// <summary>
    /// Writes a Device Configuration File (DCF) to a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static void WriteDcf(DeviceConfigurationFile dcf, Stream stream, CanOpenWriteOptions? options)
    {
        EnsureValidDcfForWrite(dcf, options);
        var writer = new DcfWriter();
        writer.WriteStream(dcf, stream);
    }

    /// <summary>
    /// Writes a Device Configuration File (DCF) to disk asynchronously.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the DCF file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    public static Task WriteDcfAsync(
        DeviceConfigurationFile dcf,
        string filePath,
        CancellationToken cancellationToken = default)
        => WriteDcfAsync(dcf, filePath, options: null, cancellationToken);

    /// <summary>
    /// Writes a Device Configuration File (DCF) to disk asynchronously.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the DCF file should be written</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static Task WriteDcfAsync(
        DeviceConfigurationFile dcf,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        EnsureValidDcfForWrite(dcf, options);
        var writer = new DcfWriter();
        return writer.WriteFileAsync(dcf, filePath, cancellationToken);
    }

    /// <summary>
    /// Writes a Device Configuration File (DCF) to a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    public static Task WriteDcfAsync(
        DeviceConfigurationFile dcf,
        Stream stream,
        CancellationToken cancellationToken = default)
        => WriteDcfAsync(dcf, stream, options: null, cancellationToken);

    /// <summary>
    /// Writes a Device Configuration File (DCF) to a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static Task WriteDcfAsync(
        DeviceConfigurationFile dcf,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        EnsureValidDcfForWrite(dcf, options);
        var writer = new DcfWriter();
        return writer.WriteStreamAsync(dcf, stream, cancellationToken);
    }

    /// <summary>
    /// Generates a DCF file content as string.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to convert</param>
    /// <returns>DCF content as string</returns>
    public static string WriteDcfToString(DeviceConfigurationFile dcf)
        => WriteDcfToString(dcf, options: null);

    /// <summary>
    /// Generates a DCF file content as string.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to convert</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <returns>DCF content as string</returns>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static string WriteDcfToString(DeviceConfigurationFile dcf, CanOpenWriteOptions? options)
    {
        EnsureValidDcfForWrite(dcf, options);
        var writer = new DcfWriter();
        return writer.GenerateString(dcf);
    }

    #endregion

    #region CPJ Read

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) file.
    /// </summary>
    /// <param name="filePath">Path to the CPJ file</param>
    /// <param name="maxInputSize">Maximum file size in bytes when reading from <paramref name="filePath"/>.</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static NodelistProject ReadCpj(
        string filePath,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Cpj.ReadFile(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) file.
    /// </summary>
    /// <param name="filePath">Path to the CPJ file</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static NodelistProject ReadCpj(string filePath, CanOpenFileOptions options)
        => Cpj.ReadFile(filePath, options);

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the CPJ file</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static Task<NodelistProject> ReadCpjAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => Cpj.ReadFileAsync(filePath, cancellationToken: cancellationToken);

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the CPJ file</param>
    /// <param name="maxInputSize">Maximum file size in bytes when reading from <paramref name="filePath"/>.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static Task<NodelistProject> ReadCpjAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Cpj.ReadFileAsync(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the CPJ file</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static Task<NodelistProject> ReadCpjAsync(
        string filePath,
        CanOpenFileOptions options,
        CancellationToken cancellationToken = default)
        => Cpj.ReadFileAsync(filePath, options, cancellationToken);

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) from a string.
    /// </summary>
    /// <param name="content">CPJ file content as string</param>
    /// <param name="maxInputSize">Maximum content length in decoded characters.</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static NodelistProject ReadCpjFromString(
        string content,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Cpj.ReadString(content, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) from a string.
    /// </summary>
    /// <param name="content">CPJ file content as string</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static NodelistProject ReadCpjFromString(string content, CanOpenFileOptions options)
        => Cpj.ReadString(content, options);

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing CPJ content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters read from <paramref name="stream"/>.</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static NodelistProject ReadCpj(
        Stream stream,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Cpj.ReadStream(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing CPJ content.</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static NodelistProject ReadCpj(Stream stream, CanOpenFileOptions options)
        => Cpj.ReadStream(stream, options);

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing CPJ content.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static Task<NodelistProject> ReadCpjAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => Cpj.ReadStreamAsync(stream, cancellationToken: cancellationToken);

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing CPJ content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters read from <paramref name="stream"/>.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static Task<NodelistProject> ReadCpjAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Cpj.ReadStreamAsync(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <summary>
    /// Reads a CiA 306-3 nodelist project (.cpj) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing CPJ content.</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed NodelistProject object</returns>
    public static Task<NodelistProject> ReadCpjAsync(
        Stream stream,
        CanOpenFileOptions options,
        CancellationToken cancellationToken = default)
        => Cpj.ReadStreamAsync(stream, options, cancellationToken);

    #endregion


    #region CPJ Write

    /// <summary>
    /// Writes a CiA 306-3 nodelist project (.cpj) to disk.
    /// </summary>
    /// <param name="cpj">The NodelistProject to write</param>
    /// <param name="filePath">Path where the CPJ file should be written</param>
    public static void WriteCpj(NodelistProject cpj, string filePath)
        => WriteCpj(cpj, filePath, options: null);

    /// <summary>
    /// Writes a CiA 306-3 nodelist project (.cpj) to disk.
    /// </summary>
    /// <param name="cpj">The NodelistProject to write</param>
    /// <param name="filePath">Path where the CPJ file should be written</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static void WriteCpj(NodelistProject cpj, string filePath, CanOpenWriteOptions? options)
    {
        EnsureValidCpjForWrite(cpj, options);
        var writer = new CpjWriter();
        writer.WriteFile(cpj, filePath);
    }

    /// <summary>
    /// Writes a CiA 306-3 nodelist project (.cpj) to a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="cpj">The NodelistProject to write</param>
    /// <param name="stream">Writable destination stream</param>
    public static void WriteCpj(NodelistProject cpj, Stream stream)
        => WriteCpj(cpj, stream, options: null);

    /// <summary>
    /// Writes a CiA 306-3 nodelist project (.cpj) to a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="cpj">The NodelistProject to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static void WriteCpj(NodelistProject cpj, Stream stream, CanOpenWriteOptions? options)
    {
        EnsureValidCpjForWrite(cpj, options);
        var writer = new CpjWriter();
        writer.WriteStream(cpj, stream);
    }

    /// <summary>
    /// Writes a CiA 306-3 nodelist project (.cpj) to disk asynchronously.
    /// </summary>
    /// <param name="cpj">The NodelistProject to write</param>
    /// <param name="filePath">Path where the CPJ file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    public static Task WriteCpjAsync(
        NodelistProject cpj,
        string filePath,
        CancellationToken cancellationToken = default)
        => WriteCpjAsync(cpj, filePath, options: null, cancellationToken);

    /// <summary>
    /// Writes a CiA 306-3 nodelist project (.cpj) to disk asynchronously.
    /// </summary>
    /// <param name="cpj">The NodelistProject to write</param>
    /// <param name="filePath">Path where the CPJ file should be written</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static Task WriteCpjAsync(
        NodelistProject cpj,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        EnsureValidCpjForWrite(cpj, options);
        var writer = new CpjWriter();
        return writer.WriteFileAsync(cpj, filePath, cancellationToken);
    }

    /// <summary>
    /// Writes a CiA 306-3 nodelist project (.cpj) to a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="cpj">The NodelistProject to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    public static Task WriteCpjAsync(
        NodelistProject cpj,
        Stream stream,
        CancellationToken cancellationToken = default)
        => WriteCpjAsync(cpj, stream, options: null, cancellationToken);

    /// <summary>
    /// Writes a CiA 306-3 nodelist project (.cpj) to a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="cpj">The NodelistProject to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static Task WriteCpjAsync(
        NodelistProject cpj,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        EnsureValidCpjForWrite(cpj, options);
        var writer = new CpjWriter();
        return writer.WriteStreamAsync(cpj, stream, cancellationToken);
    }

    /// <summary>
    /// Generates CPJ file content as string.
    /// </summary>
    /// <param name="cpj">The NodelistProject to convert</param>
    /// <returns>CPJ content as string</returns>
    public static string WriteCpjToString(NodelistProject cpj)
        => WriteCpjToString(cpj, options: null);

    /// <summary>
    /// Generates a CPJ file content as string.
    /// </summary>
    /// <param name="cpj">The NodelistProject to convert</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <returns>CPJ content as string</returns>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static string WriteCpjToString(NodelistProject cpj, CanOpenWriteOptions? options)
    {
        EnsureValidCpjForWrite(cpj, options);
        var writer = new CpjWriter();
        return writer.GenerateString(cpj);
    }

    #endregion

    #region XDD Read

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) file.
    /// </summary>
    /// <param name="filePath">Path to the XDD file</param>
    /// <param name="maxInputSize">Maximum file size in bytes when reading from <paramref name="filePath"/>.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadXdd(
        string filePath,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Xdd.ReadFile(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) file.
    /// </summary>
    /// <param name="filePath">Path to the XDD file</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadXdd(string filePath, CanOpenFileOptions options)
        => Xdd.ReadFile(filePath, options);

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the XDD file</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadXddAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => Xdd.ReadFileAsync(filePath, cancellationToken: cancellationToken);

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the XDD file</param>
    /// <param name="maxInputSize">Maximum file size in bytes when reading from <paramref name="filePath"/>.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadXddAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Xdd.ReadFileAsync(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the XDD file</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadXddAsync(
        string filePath,
        CanOpenFileOptions options,
        CancellationToken cancellationToken = default)
        => Xdd.ReadFileAsync(filePath, options, cancellationToken);

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) from a string.
    /// </summary>
    /// <param name="content">XDD file content as string</param>
    /// <param name="maxInputSize">Maximum content length in decoded characters.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadXddFromString(
        string content,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Xdd.ReadString(content, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) from a string.
    /// </summary>
    /// <param name="content">XDD file content as string</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadXddFromString(string content, CanOpenFileOptions options)
        => Xdd.ReadString(content, options);

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDD content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters read from <paramref name="stream"/>.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadXdd(
        Stream stream,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Xdd.ReadStream(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDD content.</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static ElectronicDataSheet ReadXdd(Stream stream, CanOpenFileOptions options)
        => Xdd.ReadStream(stream, options);

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDD content.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadXddAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => Xdd.ReadStreamAsync(stream, cancellationToken: cancellationToken);

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDD content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters read from <paramref name="stream"/>.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadXddAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Xdd.ReadStreamAsync(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <summary>
    /// Reads a CiA 311 XDD (XML Device Description) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDD content.</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    public static Task<ElectronicDataSheet> ReadXddAsync(
        Stream stream,
        CanOpenFileOptions options,
        CancellationToken cancellationToken = default)
        => Xdd.ReadStreamAsync(stream, options, cancellationToken);

    #endregion


    #region XDD Write

    /// <summary>
    /// Writes an ElectronicDataSheet as a CiA 311 XDD file.
    /// </summary>
    /// <param name="xdd">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the XDD file should be written</param>
    public static void WriteXdd(ElectronicDataSheet xdd, string filePath)
        => WriteXdd(xdd, filePath, options: null);

    /// <summary>
    /// Writes an ElectronicDataSheet as a CiA 311 XDD file.
    /// </summary>
    /// <param name="xdd">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the XDD file should be written</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static void WriteXdd(ElectronicDataSheet xdd, string filePath, CanOpenWriteOptions? options)
    {
        EnsureValidEdsForWrite(xdd, options);
        var writer = new XddWriter();
        writer.WriteFile(xdd, filePath);
    }

    /// <summary>
    /// Writes an ElectronicDataSheet as a CiA 311 XDD stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="xdd">The ElectronicDataSheet to write</param>
    /// <param name="stream">Writable destination stream</param>
    public static void WriteXdd(ElectronicDataSheet xdd, Stream stream)
        => WriteXdd(xdd, stream, options: null);

    /// <summary>
    /// Writes an ElectronicDataSheet as a CiA 311 XDD stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="xdd">The ElectronicDataSheet to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static void WriteXdd(ElectronicDataSheet xdd, Stream stream, CanOpenWriteOptions? options)
    {
        EnsureValidEdsForWrite(xdd, options);
        var writer = new XddWriter();
        writer.WriteStream(xdd, stream);
    }

    /// <summary>
    /// Writes an ElectronicDataSheet as a CiA 311 XDD file asynchronously.
    /// </summary>
    /// <param name="xdd">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the XDD file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    public static Task WriteXddAsync(
        ElectronicDataSheet xdd,
        string filePath,
        CancellationToken cancellationToken = default)
        => WriteXddAsync(xdd, filePath, options: null, cancellationToken);

    /// <summary>
    /// Writes an ElectronicDataSheet as a CiA 311 XDD file asynchronously.
    /// </summary>
    /// <param name="xdd">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the XDD file should be written</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static Task WriteXddAsync(
        ElectronicDataSheet xdd,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        EnsureValidEdsForWrite(xdd, options);
        var writer = new XddWriter();
        return writer.WriteFileAsync(xdd, filePath, cancellationToken);
    }

    /// <summary>
    /// Writes an ElectronicDataSheet as a CiA 311 XDD stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="xdd">The ElectronicDataSheet to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    public static Task WriteXddAsync(
        ElectronicDataSheet xdd,
        Stream stream,
        CancellationToken cancellationToken = default)
        => WriteXddAsync(xdd, stream, options: null, cancellationToken);

    /// <summary>
    /// Writes an ElectronicDataSheet as a CiA 311 XDD stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="xdd">The ElectronicDataSheet to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static Task WriteXddAsync(
        ElectronicDataSheet xdd,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        EnsureValidEdsForWrite(xdd, options);
        var writer = new XddWriter();
        return writer.WriteStreamAsync(xdd, stream, cancellationToken);
    }

    /// <summary>
    /// Generates XDD file content as string.
    /// </summary>
    /// <param name="xdd">The ElectronicDataSheet to convert</param>
    /// <returns>XDD content as string</returns>
    public static string WriteXddToString(ElectronicDataSheet xdd)
        => WriteXddToString(xdd, options: null);

    /// <summary>
    /// Generates a CiA 311 XDD file content as string.
    /// </summary>
    /// <param name="xdd">The ElectronicDataSheet to convert</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <returns>XDD content as string</returns>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static string WriteXddToString(ElectronicDataSheet xdd, CanOpenWriteOptions? options)
    {
        EnsureValidEdsForWrite(xdd, options);
        var writer = new XddWriter();
        return writer.GenerateString(xdd);
    }

    #endregion

    #region XDC Read

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) file.
    /// </summary>
    /// <param name="filePath">Path to the XDC file</param>
    /// <param name="maxInputSize">Maximum file size in bytes when reading from <paramref name="filePath"/>.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadXdc(
        string filePath,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Xdc.ReadFile(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) file.
    /// </summary>
    /// <param name="filePath">Path to the XDC file</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadXdc(string filePath, CanOpenFileOptions options)
        => Xdc.ReadFile(filePath, options);

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the XDC file</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadXdcAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => Xdc.ReadFileAsync(filePath, cancellationToken: cancellationToken);

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the XDC file</param>
    /// <param name="maxInputSize">Maximum file size in bytes when reading from <paramref name="filePath"/>.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadXdcAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Xdc.ReadFileAsync(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the XDC file</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadXdcAsync(
        string filePath,
        CanOpenFileOptions options,
        CancellationToken cancellationToken = default)
        => Xdc.ReadFileAsync(filePath, options, cancellationToken);

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) from a string.
    /// </summary>
    /// <param name="content">XDC file content as string</param>
    /// <param name="maxInputSize">Maximum content length in decoded characters.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadXdcFromString(
        string content,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Xdc.ReadString(content, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) from a string.
    /// </summary>
    /// <param name="content">XDC file content as string</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadXdcFromString(string content, CanOpenFileOptions options)
        => Xdc.ReadString(content, options);

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDC content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters read from <paramref name="stream"/>.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadXdc(
        Stream stream,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Xdc.ReadStream(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) from a stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDC content.</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static DeviceConfigurationFile ReadXdc(Stream stream, CanOpenFileOptions options)
        => Xdc.ReadStream(stream, options);

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDC content.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadXdcAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => Xdc.ReadStreamAsync(stream, cancellationToken: cancellationToken);

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDC content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters read from <paramref name="stream"/>.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadXdcAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Xdc.ReadStreamAsync(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <summary>
    /// Reads a CiA 311 XDC (XML Device Configuration) from a stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="stream">Readable stream containing XDC content.</param>
    /// <param name="options">Read options such as input size limits.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public static Task<DeviceConfigurationFile> ReadXdcAsync(
        Stream stream,
        CanOpenFileOptions options,
        CancellationToken cancellationToken = default)
        => Xdc.ReadStreamAsync(stream, options, cancellationToken);

    #endregion


    #region XDC Write

    /// <summary>
    /// Writes a DeviceConfigurationFile as a CiA 311 XDC file.
    /// </summary>
    /// <param name="xdc">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the XDC file should be written</param>
    public static void WriteXdc(DeviceConfigurationFile xdc, string filePath)
        => WriteXdc(xdc, filePath, options: null);

    /// <summary>
    /// Writes a DeviceConfigurationFile as a CiA 311 XDC file.
    /// </summary>
    /// <param name="xdc">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the XDC file should be written</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static void WriteXdc(DeviceConfigurationFile xdc, string filePath, CanOpenWriteOptions? options)
    {
        EnsureValidDcfForWrite(xdc, options);
        var writer = new XdcWriter();
        writer.WriteFile(xdc, filePath);
    }

    /// <summary>
    /// Writes a DeviceConfigurationFile as a CiA 311 XDC stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="xdc">The DeviceConfigurationFile to write</param>
    /// <param name="stream">Writable destination stream</param>
    public static void WriteXdc(DeviceConfigurationFile xdc, Stream stream)
        => WriteXdc(xdc, stream, options: null);

    /// <summary>
    /// Writes a DeviceConfigurationFile as a CiA 311 XDC stream.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="xdc">The DeviceConfigurationFile to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static void WriteXdc(DeviceConfigurationFile xdc, Stream stream, CanOpenWriteOptions? options)
    {
        EnsureValidDcfForWrite(xdc, options);
        var writer = new XdcWriter();
        writer.WriteStream(xdc, stream);
    }

    /// <summary>
    /// Writes a DeviceConfigurationFile as a CiA 311 XDC file asynchronously.
    /// </summary>
    /// <param name="xdc">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the XDC file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    public static Task WriteXdcAsync(
        DeviceConfigurationFile xdc,
        string filePath,
        CancellationToken cancellationToken = default)
        => WriteXdcAsync(xdc, filePath, options: null, cancellationToken);

    /// <summary>
    /// Writes a DeviceConfigurationFile as a CiA 311 XDC file asynchronously.
    /// </summary>
    /// <param name="xdc">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the XDC file should be written</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static Task WriteXdcAsync(
        DeviceConfigurationFile xdc,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        EnsureValidDcfForWrite(xdc, options);
        var writer = new XdcWriter();
        return writer.WriteFileAsync(xdc, filePath, cancellationToken);
    }

    /// <summary>
    /// Writes a DeviceConfigurationFile as a CiA 311 XDC stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="xdc">The DeviceConfigurationFile to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    public static Task WriteXdcAsync(
        DeviceConfigurationFile xdc,
        Stream stream,
        CancellationToken cancellationToken = default)
        => WriteXdcAsync(xdc, stream, options: null, cancellationToken);

    /// <summary>
    /// Writes a DeviceConfigurationFile as a CiA 311 XDC stream asynchronously.
    /// The stream is not disposed by this method.
    /// </summary>
    /// <param name="xdc">The DeviceConfigurationFile to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static Task WriteXdcAsync(
        DeviceConfigurationFile xdc,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        EnsureValidDcfForWrite(xdc, options);
        var writer = new XdcWriter();
        return writer.WriteStreamAsync(xdc, stream, cancellationToken);
    }

    /// <summary>
    /// Generates XDC file content as string.
    /// </summary>
    /// <param name="xdc">The DeviceConfigurationFile to convert</param>
    /// <returns>XDC content as string</returns>
    public static string WriteXdcToString(DeviceConfigurationFile xdc)
        => WriteXdcToString(xdc, options: null);

    /// <summary>
    /// Generates a CiA 311 XDC file content as string.
    /// </summary>
    /// <param name="xdc">The DeviceConfigurationFile to convert</param>
    /// <param name="options">Optional write behavior such as pre-write validation.</param>
    /// <returns>XDC content as string</returns>
    /// <exception cref="ModelValidationException">
    /// Thrown when <paramref name="options"/>.<see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is
    /// <see langword="true"/> and the model has validation issues.
    /// </exception>
    public static string WriteXdcToString(DeviceConfigurationFile xdc, CanOpenWriteOptions? options)
    {
        EnsureValidDcfForWrite(xdc, options);
        var writer = new XdcWriter();
        return writer.GenerateString(xdc);
    }

    #endregion

    #region EDS to DCF Conversion

    /// <summary>
    /// Converts an EDS to a DCF with specified commissioning parameters.
    /// </summary>
    /// <param name="eds">The EDS to convert</param>
    /// <param name="nodeId">Node ID for the device</param>
    /// <param name="baudrate">Baudrate in kbit/s (default: 250)</param>
    /// <param name="nodeName">Optional node name</param>
    /// <remarks>
    /// Uses <see cref="DateTime.UtcNow"/> for generated FileInfo date/time fields.
    /// Use the overload with explicit timestamp for fully deterministic output.
    /// </remarks>
    /// <returns>A new DeviceConfigurationFile</returns>
    /// <example>
    /// <code>
    /// var eds = CanOpenFile.ReadEds("device.eds");
    /// var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 2, baudrate: 500, nodeName: "MyDevice");
    /// CanOpenFile.WriteDcf(dcf, "device_node2.dcf");
    /// </code>
    /// </example>
    public static DeviceConfigurationFile EdsToDcf(
        ElectronicDataSheet eds,
        byte nodeId,
        ushort baudrate = 250,
        string? nodeName = null)
    {
        return EdsToDcf(eds, nodeId, DateTime.UtcNow, baudrate, nodeName);
    }

    /// <summary>
    /// Converts an EDS to a DCF with specified commissioning parameters and an explicit timestamp.
    /// </summary>
    /// <param name="eds">The EDS to convert</param>
    /// <param name="nodeId">Node ID for the device</param>
    /// <param name="timestamp">Timestamp used for generated FileInfo creation date/time fields.</param>
    /// <param name="baudrate">Baudrate in kbit/s (default: 250)</param>
    /// <param name="nodeName">Optional node name</param>
    /// <remarks>
    /// The timestamp is formatted with invariant culture as <c>MM-dd-yyyy</c> for
    /// <c>CreationDate</c> and <c>hh:mmtt</c> for <c>CreationTime</c>; no timezone
    /// conversion is applied.
    /// </remarks>
    /// <returns>A new DeviceConfigurationFile</returns>
    public static DeviceConfigurationFile EdsToDcf(
        ElectronicDataSheet eds,
        byte nodeId,
        DateTime timestamp,
        ushort baudrate = 250,
        string? nodeName = null)
    {
        if (nodeId < 1 || nodeId > 127)
            throw new ArgumentOutOfRangeException(nameof(nodeId), nodeId, "CANopen Node-ID must be in range 1..127.");

        var dcf = new DeviceConfigurationFile
        {
            FileInfo = new Models.EdsFileInfo
            {
                FileName = Path.ChangeExtension(eds.FileInfo.FileName, ".dcf"),
                FileVersion = eds.FileInfo.FileVersion,
                FileRevision = (byte)(eds.FileInfo.FileRevision + 1),
                EdsVersion = eds.FileInfo.EdsVersion,
                Description = $"DCF generated from {eds.FileInfo.FileName}",
                CreationDate = timestamp.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture),
                CreationTime = timestamp.ToString("hh:mmtt", CultureInfo.InvariantCulture),
                CreatedBy = "EdsDcfNet Library",
                LastEds = eds.FileInfo.FileName
            },
            DeviceInfo = ModelCloner.CloneDeviceInfo(eds.DeviceInfo),
            DeviceCommissioning = new DeviceCommissioning
            {
                NodeId = nodeId,
                Baudrate = baudrate,
                NodeName = nodeName ?? $"{eds.DeviceInfo.ProductName}_Node{nodeId}",
                NetNumber = 1,
                NetworkName = "CANopen Network",
                CANopenManager = false
            },
            ObjectDictionary = ModelCloner.CloneObjectDictionary(eds.ObjectDictionary),
            Comments = ModelCloner.CloneComments(eds.Comments),
            DynamicChannels = ModelCloner.CloneDynamicChannels(eds.DynamicChannels),
            ApplicationProcess = ModelCloner.CloneApplicationProcess(eds.ApplicationProcess)
        };

        dcf.SupportedModules.AddRange(ModelCloner.CloneSupportedModules(eds.SupportedModules));
        dcf.Tools.AddRange(ModelCloner.CloneTools(eds.Tools));
        foreach (var kvp in ModelCloner.CloneAdditionalSections(eds.AdditionalSections))
            dcf.AdditionalSections[kvp.Key] = kvp.Value;

        return dcf;
    }

    #endregion

    #region Write validation helpers

    private static void EnsureValidEdsForWrite(ElectronicDataSheet eds, CanOpenWriteOptions? options)
    {
        if (options?.ValidateBeforeWrite == true)
            EnsureValid(eds);
    }

    private static void EnsureValidDcfForWrite(DeviceConfigurationFile dcf, CanOpenWriteOptions? options)
    {
        if (options?.ValidateBeforeWrite == true)
            EnsureValid(dcf);
    }

    private static void EnsureValidCpjForWrite(NodelistProject cpj, CanOpenWriteOptions? options)
    {
        if (options?.ValidateBeforeWrite == true)
            EnsureValid(cpj);
    }

    private static void ThrowIfInvalid(IReadOnlyList<ValidationIssue> issues)
    {
        if (issues.Count > 0)
            throw new ModelValidationException(issues);
    }

    #endregion
}
