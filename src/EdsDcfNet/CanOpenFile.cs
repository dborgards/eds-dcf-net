namespace EdsDcfNet;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Validation;

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
/// For format-specific operations with shared options, use the canonical entry points
/// <see cref="Eds"/>, <see cref="Dcf"/>, <see cref="Cpj"/>, <see cref="Xdd"/>, and <see cref="Xdc"/>.
/// Legacy <c>Read*</c>/<c>Write*</c> static overloads remain for backward compatibility; they delegate
/// to these entry points and default-parameter-only write overloads are marked obsolete (advisory).
/// </remarks>
public static class CanOpenFile
{
    /// <summary>
    /// EDS read/write operations. Prefer this entry point for new code that needs
    /// <see cref="CanOpenFileOptions"/> and <see cref="CanOpenWriteOptions"/> instead of additional
    /// <see cref="CanOpenFile"/> overloads.
    /// </summary>
    public static EdsCanOpenOperations Eds { get; } = EdsCanOpenOperations.Instance;
    /// <summary>
    /// DCF read/write operations. Prefer this entry point for new code that needs
    /// <see cref="CanOpenFileOptions"/> and <see cref="CanOpenWriteOptions"/>.
    /// </summary>
    public static DcfCanOpenOperations Dcf { get; } = DcfCanOpenOperations.Instance;

    /// <summary>
    /// CPJ read/write operations. Prefer this entry point for new code that needs
    /// <see cref="CanOpenFileOptions"/> and <see cref="CanOpenWriteOptions"/>.
    /// </summary>
    public static CpjCanOpenOperations Cpj { get; } = CpjCanOpenOperations.Instance;

    /// <summary>
    /// XDD read/write operations. Prefer this entry point for new code that needs
    /// <see cref="CanOpenFileOptions"/> and <see cref="CanOpenWriteOptions"/>.
    /// </summary>
    public static XddCanOpenOperations Xdd { get; } = XddCanOpenOperations.Instance;

    /// <summary>
    /// XDC read/write operations. Prefer this entry point for new code that needs
    /// <see cref="CanOpenFileOptions"/> and <see cref="CanOpenWriteOptions"/>.
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

    /// <inheritdoc cref="EdsCanOpenOperations.ReadFile"/>
    public static ElectronicDataSheet ReadEds(
        string filePath,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Eds.ReadFile(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <inheritdoc cref="EdsCanOpenOperations.ReadFileAsync"/>
    public static Task<ElectronicDataSheet> ReadEdsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => Eds.ReadFileAsync(filePath, cancellationToken: cancellationToken);

    /// <inheritdoc cref="EdsCanOpenOperations.ReadFileAsync"/>
    public static Task<ElectronicDataSheet> ReadEdsAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Eds.ReadFileAsync(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <inheritdoc cref="EdsCanOpenOperations.ReadString"/>
    public static ElectronicDataSheet ReadEdsFromString(
        string content,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Eds.ReadString(content, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <inheritdoc cref="EdsCanOpenOperations.ReadStream"/>
    public static ElectronicDataSheet ReadEds(
        Stream stream,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Eds.ReadStream(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <inheritdoc cref="EdsCanOpenOperations.ReadStreamAsync"/>
    public static Task<ElectronicDataSheet> ReadEdsAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => Eds.ReadStreamAsync(stream, cancellationToken: cancellationToken);

    /// <inheritdoc cref="EdsCanOpenOperations.ReadStreamAsync"/>
    public static Task<ElectronicDataSheet> ReadEdsAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Eds.ReadStreamAsync(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    #endregion

    #region EDS Write

        /// <inheritdoc cref="EdsCanOpenOperations.WriteFile(ElectronicDataSheet, string)"/>


    [Obsolete("Use CanOpenFile.Eds.WriteFile instead.")]
    public static void WriteEds(ElectronicDataSheet eds, string filePath)
        => Eds.WriteFile(eds, filePath, options: null);

    /// <inheritdoc cref="EdsCanOpenOperations.WriteFile(ElectronicDataSheet, string, CanOpenWriteOptions?)"/>
    public static void WriteEds(ElectronicDataSheet eds, string filePath, CanOpenWriteOptions? options)
        => Eds.WriteFile(eds, filePath, options);

        /// <inheritdoc cref="EdsCanOpenOperations.WriteStream(ElectronicDataSheet, Stream)"/>


    [Obsolete("Use CanOpenFile.Eds.WriteStream instead.")]
    public static void WriteEds(ElectronicDataSheet eds, Stream stream)
        => Eds.WriteStream(eds, stream);

    /// <inheritdoc cref="EdsCanOpenOperations.WriteStream(ElectronicDataSheet, Stream, CanOpenWriteOptions?)"/>
    public static void WriteEds(ElectronicDataSheet eds, Stream stream, CanOpenWriteOptions? options)
        => Eds.WriteStream(eds, stream, options);

        /// <inheritdoc cref="EdsCanOpenOperations.WriteFileAsync(ElectronicDataSheet, string, CancellationToken)"/>


    [Obsolete("Use CanOpenFile.Eds.WriteFileAsync instead.")]
    public static Task WriteEdsAsync(
        ElectronicDataSheet eds,
        string filePath,
        CancellationToken cancellationToken = default)
        => Eds.WriteFileAsync(eds, filePath, cancellationToken: cancellationToken);

    /// <inheritdoc cref="EdsCanOpenOperations.WriteFileAsync(ElectronicDataSheet, string, CanOpenWriteOptions?, CancellationToken)"/>
    public static Task WriteEdsAsync(
        ElectronicDataSheet eds,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => Eds.WriteFileAsync(eds, filePath, options, cancellationToken);

        /// <inheritdoc cref="EdsCanOpenOperations.WriteStreamAsync(ElectronicDataSheet, Stream, CancellationToken)"/>


    [Obsolete("Use CanOpenFile.Eds.WriteStreamAsync instead.")]
    public static Task WriteEdsAsync(
        ElectronicDataSheet eds,
        Stream stream,
        CancellationToken cancellationToken = default)
        => Eds.WriteStreamAsync(eds, stream, cancellationToken: cancellationToken);

    /// <inheritdoc cref="EdsCanOpenOperations.WriteStreamAsync(ElectronicDataSheet, Stream, CanOpenWriteOptions?, CancellationToken)"/>
    public static Task WriteEdsAsync(
        ElectronicDataSheet eds,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => Eds.WriteStreamAsync(eds, stream, options, cancellationToken);

        /// <inheritdoc cref="EdsCanOpenOperations.WriteToString(ElectronicDataSheet)"/>


    [Obsolete("Use CanOpenFile.Eds.WriteToString instead.")]
    public static string WriteEdsToString(ElectronicDataSheet eds)
        => Eds.WriteToString(eds, options: null);

    /// <inheritdoc cref="EdsCanOpenOperations.WriteToString(ElectronicDataSheet, CanOpenWriteOptions?)"/>
    public static string WriteEdsToString(ElectronicDataSheet eds, CanOpenWriteOptions? options)
        => Eds.WriteToString(eds, options);

    #endregion

    #region DCF Read

    /// <inheritdoc cref="DcfCanOpenOperations.ReadFile"/>
    public static DeviceConfigurationFile ReadDcf(
        string filePath,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Dcf.ReadFile(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <inheritdoc cref="DcfCanOpenOperations.ReadFile"/>
    public static DeviceConfigurationFile ReadDcf(string filePath, CanOpenFileOptions options)
        => Dcf.ReadFile(filePath, options);

    /// <inheritdoc cref="DcfCanOpenOperations.ReadFileAsync"/>
    public static Task<DeviceConfigurationFile> ReadDcfAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => Dcf.ReadFileAsync(filePath, cancellationToken: cancellationToken);

    /// <inheritdoc cref="DcfCanOpenOperations.ReadFileAsync"/>
    public static Task<DeviceConfigurationFile> ReadDcfAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Dcf.ReadFileAsync(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <inheritdoc cref="DcfCanOpenOperations.ReadFileAsync"/>
    public static Task<DeviceConfigurationFile> ReadDcfAsync(
        string filePath,
        CanOpenFileOptions options,
        CancellationToken cancellationToken = default)
        => Dcf.ReadFileAsync(filePath, options, cancellationToken);

    /// <inheritdoc cref="DcfCanOpenOperations.ReadString"/>
    public static DeviceConfigurationFile ReadDcfFromString(
        string content,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Dcf.ReadString(content, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <inheritdoc cref="DcfCanOpenOperations.ReadString"/>
    public static DeviceConfigurationFile ReadDcfFromString(string content, CanOpenFileOptions options)
        => Dcf.ReadString(content, options);

    /// <inheritdoc cref="DcfCanOpenOperations.ReadStream"/>
    public static DeviceConfigurationFile ReadDcf(
        Stream stream,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Dcf.ReadStream(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <inheritdoc cref="DcfCanOpenOperations.ReadStream"/>
    public static DeviceConfigurationFile ReadDcf(Stream stream, CanOpenFileOptions options)
        => Dcf.ReadStream(stream, options);

    /// <inheritdoc cref="DcfCanOpenOperations.ReadStreamAsync"/>
    public static Task<DeviceConfigurationFile> ReadDcfAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => Dcf.ReadStreamAsync(stream, cancellationToken: cancellationToken);

    /// <inheritdoc cref="DcfCanOpenOperations.ReadStreamAsync"/>
    public static Task<DeviceConfigurationFile> ReadDcfAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Dcf.ReadStreamAsync(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <inheritdoc cref="DcfCanOpenOperations.ReadStreamAsync"/>
    public static Task<DeviceConfigurationFile> ReadDcfAsync(
        Stream stream,
        CanOpenFileOptions options,
        CancellationToken cancellationToken = default)
        => Dcf.ReadStreamAsync(stream, options, cancellationToken);

    #endregion


    #region DCF Write

        /// <inheritdoc cref="DcfCanOpenOperations.WriteFile(DeviceConfigurationFile, string)"/>


    [Obsolete("Use CanOpenFile.Dcf.WriteFile instead.")]
    public static void WriteDcf(DeviceConfigurationFile dcf, string filePath)
        => Dcf.WriteFile(dcf, filePath, options: null);

    /// <inheritdoc cref="DcfCanOpenOperations.WriteFile(DeviceConfigurationFile, string, CanOpenWriteOptions?)"/>
    public static void WriteDcf(DeviceConfigurationFile dcf, string filePath, CanOpenWriteOptions? options)
        => Dcf.WriteFile(dcf, filePath, options);

        /// <inheritdoc cref="DcfCanOpenOperations.WriteStream(DeviceConfigurationFile, Stream)"/>


    [Obsolete("Use CanOpenFile.Dcf.WriteStream instead.")]
    public static void WriteDcf(DeviceConfigurationFile dcf, Stream stream)
        => Dcf.WriteStream(dcf, stream);

    /// <inheritdoc cref="DcfCanOpenOperations.WriteStream(DeviceConfigurationFile, Stream, CanOpenWriteOptions?)"/>
    public static void WriteDcf(DeviceConfigurationFile dcf, Stream stream, CanOpenWriteOptions? options)
        => Dcf.WriteStream(dcf, stream, options);

        /// <inheritdoc cref="DcfCanOpenOperations.WriteFileAsync(DeviceConfigurationFile, string, CancellationToken)"/>


    [Obsolete("Use CanOpenFile.Dcf.WriteFileAsync instead.")]
    public static Task WriteDcfAsync(
        DeviceConfigurationFile dcf,
        string filePath,
        CancellationToken cancellationToken = default)
        => Dcf.WriteFileAsync(dcf, filePath, cancellationToken: cancellationToken);

    /// <inheritdoc cref="DcfCanOpenOperations.WriteFileAsync(DeviceConfigurationFile, string, CanOpenWriteOptions?, CancellationToken)"/>
    public static Task WriteDcfAsync(
        DeviceConfigurationFile dcf,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => Dcf.WriteFileAsync(dcf, filePath, options, cancellationToken);

        /// <inheritdoc cref="DcfCanOpenOperations.WriteStreamAsync(DeviceConfigurationFile, Stream, CancellationToken)"/>


    [Obsolete("Use CanOpenFile.Dcf.WriteStreamAsync instead.")]
    public static Task WriteDcfAsync(
        DeviceConfigurationFile dcf,
        Stream stream,
        CancellationToken cancellationToken = default)
        => Dcf.WriteStreamAsync(dcf, stream, cancellationToken: cancellationToken);

    /// <inheritdoc cref="DcfCanOpenOperations.WriteStreamAsync(DeviceConfigurationFile, Stream, CanOpenWriteOptions?, CancellationToken)"/>
    public static Task WriteDcfAsync(
        DeviceConfigurationFile dcf,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => Dcf.WriteStreamAsync(dcf, stream, options, cancellationToken);

        /// <inheritdoc cref="DcfCanOpenOperations.WriteToString(DeviceConfigurationFile)"/>


    [Obsolete("Use CanOpenFile.Dcf.WriteToString instead.")]
    public static string WriteDcfToString(DeviceConfigurationFile dcf)
        => Dcf.WriteToString(dcf, options: null);

    /// <inheritdoc cref="DcfCanOpenOperations.WriteToString(DeviceConfigurationFile, CanOpenWriteOptions?)"/>
    public static string WriteDcfToString(DeviceConfigurationFile dcf, CanOpenWriteOptions? options)
        => Dcf.WriteToString(dcf, options);

    #endregion

    #region CPJ Read

    /// <inheritdoc cref="CpjCanOpenOperations.ReadFile"/>
    public static NodelistProject ReadCpj(
        string filePath,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Cpj.ReadFile(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <inheritdoc cref="CpjCanOpenOperations.ReadFile"/>
    public static NodelistProject ReadCpj(string filePath, CanOpenFileOptions options)
        => Cpj.ReadFile(filePath, options);

    /// <inheritdoc cref="CpjCanOpenOperations.ReadFileAsync"/>
    public static Task<NodelistProject> ReadCpjAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => Cpj.ReadFileAsync(filePath, cancellationToken: cancellationToken);

    /// <inheritdoc cref="CpjCanOpenOperations.ReadFileAsync"/>
    public static Task<NodelistProject> ReadCpjAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Cpj.ReadFileAsync(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <inheritdoc cref="CpjCanOpenOperations.ReadFileAsync"/>
    public static Task<NodelistProject> ReadCpjAsync(
        string filePath,
        CanOpenFileOptions options,
        CancellationToken cancellationToken = default)
        => Cpj.ReadFileAsync(filePath, options, cancellationToken);

    /// <inheritdoc cref="CpjCanOpenOperations.ReadString"/>
    public static NodelistProject ReadCpjFromString(
        string content,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Cpj.ReadString(content, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <inheritdoc cref="CpjCanOpenOperations.ReadString"/>
    public static NodelistProject ReadCpjFromString(string content, CanOpenFileOptions options)
        => Cpj.ReadString(content, options);

    /// <inheritdoc cref="CpjCanOpenOperations.ReadStream"/>
    public static NodelistProject ReadCpj(
        Stream stream,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Cpj.ReadStream(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <inheritdoc cref="CpjCanOpenOperations.ReadStream"/>
    public static NodelistProject ReadCpj(Stream stream, CanOpenFileOptions options)
        => Cpj.ReadStream(stream, options);

    /// <inheritdoc cref="CpjCanOpenOperations.ReadStreamAsync"/>
    public static Task<NodelistProject> ReadCpjAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => Cpj.ReadStreamAsync(stream, cancellationToken: cancellationToken);

    /// <inheritdoc cref="CpjCanOpenOperations.ReadStreamAsync"/>
    public static Task<NodelistProject> ReadCpjAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Cpj.ReadStreamAsync(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <inheritdoc cref="CpjCanOpenOperations.ReadStreamAsync"/>
    public static Task<NodelistProject> ReadCpjAsync(
        Stream stream,
        CanOpenFileOptions options,
        CancellationToken cancellationToken = default)
        => Cpj.ReadStreamAsync(stream, options, cancellationToken);

    #endregion


    #region CPJ Write

        /// <inheritdoc cref="CpjCanOpenOperations.WriteFile(NodelistProject, string)"/>


    [Obsolete("Use CanOpenFile.Cpj.WriteFile instead.")]
    public static void WriteCpj(NodelistProject cpj, string filePath)
        => Cpj.WriteFile(cpj, filePath, options: null);

    /// <inheritdoc cref="CpjCanOpenOperations.WriteFile(NodelistProject, string, CanOpenWriteOptions?)"/>
    public static void WriteCpj(NodelistProject cpj, string filePath, CanOpenWriteOptions? options)
        => Cpj.WriteFile(cpj, filePath, options);

        /// <inheritdoc cref="CpjCanOpenOperations.WriteStream(NodelistProject, Stream)"/>


    [Obsolete("Use CanOpenFile.Cpj.WriteStream instead.")]
    public static void WriteCpj(NodelistProject cpj, Stream stream)
        => Cpj.WriteStream(cpj, stream);

    /// <inheritdoc cref="CpjCanOpenOperations.WriteStream(NodelistProject, Stream, CanOpenWriteOptions?)"/>
    public static void WriteCpj(NodelistProject cpj, Stream stream, CanOpenWriteOptions? options)
        => Cpj.WriteStream(cpj, stream, options);

        /// <inheritdoc cref="CpjCanOpenOperations.WriteFileAsync(NodelistProject, string, CancellationToken)"/>


    [Obsolete("Use CanOpenFile.Cpj.WriteFileAsync instead.")]
    public static Task WriteCpjAsync(
        NodelistProject cpj,
        string filePath,
        CancellationToken cancellationToken = default)
        => Cpj.WriteFileAsync(cpj, filePath, cancellationToken: cancellationToken);

    /// <inheritdoc cref="CpjCanOpenOperations.WriteFileAsync(NodelistProject, string, CanOpenWriteOptions?, CancellationToken)"/>
    public static Task WriteCpjAsync(
        NodelistProject cpj,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => Cpj.WriteFileAsync(cpj, filePath, options, cancellationToken);

        /// <inheritdoc cref="CpjCanOpenOperations.WriteStreamAsync(NodelistProject, Stream, CancellationToken)"/>


    [Obsolete("Use CanOpenFile.Cpj.WriteStreamAsync instead.")]
    public static Task WriteCpjAsync(
        NodelistProject cpj,
        Stream stream,
        CancellationToken cancellationToken = default)
        => Cpj.WriteStreamAsync(cpj, stream, cancellationToken: cancellationToken);

    /// <inheritdoc cref="CpjCanOpenOperations.WriteStreamAsync(NodelistProject, Stream, CanOpenWriteOptions?, CancellationToken)"/>
    public static Task WriteCpjAsync(
        NodelistProject cpj,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => Cpj.WriteStreamAsync(cpj, stream, options, cancellationToken);

        /// <inheritdoc cref="CpjCanOpenOperations.WriteToString(NodelistProject)"/>


    [Obsolete("Use CanOpenFile.Cpj.WriteToString instead.")]
    public static string WriteCpjToString(NodelistProject cpj)
        => Cpj.WriteToString(cpj, options: null);

    /// <inheritdoc cref="CpjCanOpenOperations.WriteToString(NodelistProject, CanOpenWriteOptions?)"/>
    public static string WriteCpjToString(NodelistProject cpj, CanOpenWriteOptions? options)
        => Cpj.WriteToString(cpj, options);

    #endregion

    #region XDD Read

    /// <inheritdoc cref="XddCanOpenOperations.ReadFile"/>
    public static ElectronicDataSheet ReadXdd(
        string filePath,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Xdd.ReadFile(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <inheritdoc cref="XddCanOpenOperations.ReadFile"/>
    public static ElectronicDataSheet ReadXdd(string filePath, CanOpenFileOptions options)
        => Xdd.ReadFile(filePath, options);

    /// <inheritdoc cref="XddCanOpenOperations.ReadFileAsync"/>
    public static Task<ElectronicDataSheet> ReadXddAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => Xdd.ReadFileAsync(filePath, cancellationToken: cancellationToken);

    /// <inheritdoc cref="XddCanOpenOperations.ReadFileAsync"/>
    public static Task<ElectronicDataSheet> ReadXddAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Xdd.ReadFileAsync(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <inheritdoc cref="XddCanOpenOperations.ReadFileAsync"/>
    public static Task<ElectronicDataSheet> ReadXddAsync(
        string filePath,
        CanOpenFileOptions options,
        CancellationToken cancellationToken = default)
        => Xdd.ReadFileAsync(filePath, options, cancellationToken);

    /// <inheritdoc cref="XddCanOpenOperations.ReadString"/>
    public static ElectronicDataSheet ReadXddFromString(
        string content,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Xdd.ReadString(content, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <inheritdoc cref="XddCanOpenOperations.ReadString"/>
    public static ElectronicDataSheet ReadXddFromString(string content, CanOpenFileOptions options)
        => Xdd.ReadString(content, options);

    /// <inheritdoc cref="XddCanOpenOperations.ReadStream"/>
    public static ElectronicDataSheet ReadXdd(
        Stream stream,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Xdd.ReadStream(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <inheritdoc cref="XddCanOpenOperations.ReadStream"/>
    public static ElectronicDataSheet ReadXdd(Stream stream, CanOpenFileOptions options)
        => Xdd.ReadStream(stream, options);

    /// <inheritdoc cref="XddCanOpenOperations.ReadStreamAsync"/>
    public static Task<ElectronicDataSheet> ReadXddAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => Xdd.ReadStreamAsync(stream, cancellationToken: cancellationToken);

    /// <inheritdoc cref="XddCanOpenOperations.ReadStreamAsync"/>
    public static Task<ElectronicDataSheet> ReadXddAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Xdd.ReadStreamAsync(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <inheritdoc cref="XddCanOpenOperations.ReadStreamAsync"/>
    public static Task<ElectronicDataSheet> ReadXddAsync(
        Stream stream,
        CanOpenFileOptions options,
        CancellationToken cancellationToken = default)
        => Xdd.ReadStreamAsync(stream, options, cancellationToken);

    #endregion


    #region XDD Write

        /// <inheritdoc cref="XddCanOpenOperations.WriteFile(ElectronicDataSheet, string)"/>


    [Obsolete("Use CanOpenFile.Xdd.WriteFile instead.")]
    public static void WriteXdd(ElectronicDataSheet xdd, string filePath)
        => Xdd.WriteFile(xdd, filePath, options: null);

    /// <inheritdoc cref="XddCanOpenOperations.WriteFile(ElectronicDataSheet, string, CanOpenWriteOptions?)"/>
    public static void WriteXdd(ElectronicDataSheet xdd, string filePath, CanOpenWriteOptions? options)
        => Xdd.WriteFile(xdd, filePath, options);

        /// <inheritdoc cref="XddCanOpenOperations.WriteStream(ElectronicDataSheet, Stream)"/>


    [Obsolete("Use CanOpenFile.Xdd.WriteStream instead.")]
    public static void WriteXdd(ElectronicDataSheet xdd, Stream stream)
        => Xdd.WriteStream(xdd, stream);

    /// <inheritdoc cref="XddCanOpenOperations.WriteStream(ElectronicDataSheet, Stream, CanOpenWriteOptions?)"/>
    public static void WriteXdd(ElectronicDataSheet xdd, Stream stream, CanOpenWriteOptions? options)
        => Xdd.WriteStream(xdd, stream, options);

        /// <inheritdoc cref="XddCanOpenOperations.WriteFileAsync(ElectronicDataSheet, string, CancellationToken)"/>


    [Obsolete("Use CanOpenFile.Xdd.WriteFileAsync instead.")]
    public static Task WriteXddAsync(
        ElectronicDataSheet xdd,
        string filePath,
        CancellationToken cancellationToken = default)
        => Xdd.WriteFileAsync(xdd, filePath, cancellationToken: cancellationToken);

    /// <inheritdoc cref="XddCanOpenOperations.WriteFileAsync(ElectronicDataSheet, string, CanOpenWriteOptions?, CancellationToken)"/>
    public static Task WriteXddAsync(
        ElectronicDataSheet xdd,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => Xdd.WriteFileAsync(xdd, filePath, options, cancellationToken);

        /// <inheritdoc cref="XddCanOpenOperations.WriteStreamAsync(ElectronicDataSheet, Stream, CancellationToken)"/>


    [Obsolete("Use CanOpenFile.Xdd.WriteStreamAsync instead.")]
    public static Task WriteXddAsync(
        ElectronicDataSheet xdd,
        Stream stream,
        CancellationToken cancellationToken = default)
        => Xdd.WriteStreamAsync(xdd, stream, cancellationToken: cancellationToken);

    /// <inheritdoc cref="XddCanOpenOperations.WriteStreamAsync(ElectronicDataSheet, Stream, CanOpenWriteOptions?, CancellationToken)"/>
    public static Task WriteXddAsync(
        ElectronicDataSheet xdd,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => Xdd.WriteStreamAsync(xdd, stream, options, cancellationToken);

        /// <inheritdoc cref="XddCanOpenOperations.WriteToString(ElectronicDataSheet)"/>


    [Obsolete("Use CanOpenFile.Xdd.WriteToString instead.")]
    public static string WriteXddToString(ElectronicDataSheet xdd)
        => Xdd.WriteToString(xdd, options: null);

    /// <inheritdoc cref="XddCanOpenOperations.WriteToString(ElectronicDataSheet, CanOpenWriteOptions?)"/>
    public static string WriteXddToString(ElectronicDataSheet xdd, CanOpenWriteOptions? options)
        => Xdd.WriteToString(xdd, options);

    #endregion

    #region XDC Read

    /// <inheritdoc cref="XdcCanOpenOperations.ReadFile"/>
    public static DeviceConfigurationFile ReadXdc(
        string filePath,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Xdc.ReadFile(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <inheritdoc cref="XdcCanOpenOperations.ReadFile"/>
    public static DeviceConfigurationFile ReadXdc(string filePath, CanOpenFileOptions options)
        => Xdc.ReadFile(filePath, options);

    /// <inheritdoc cref="XdcCanOpenOperations.ReadFileAsync"/>
    public static Task<DeviceConfigurationFile> ReadXdcAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => Xdc.ReadFileAsync(filePath, cancellationToken: cancellationToken);

    /// <inheritdoc cref="XdcCanOpenOperations.ReadFileAsync"/>
    public static Task<DeviceConfigurationFile> ReadXdcAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Xdc.ReadFileAsync(filePath, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <inheritdoc cref="XdcCanOpenOperations.ReadFileAsync"/>
    public static Task<DeviceConfigurationFile> ReadXdcAsync(
        string filePath,
        CanOpenFileOptions options,
        CancellationToken cancellationToken = default)
        => Xdc.ReadFileAsync(filePath, options, cancellationToken);

    /// <inheritdoc cref="XdcCanOpenOperations.ReadString"/>
    public static DeviceConfigurationFile ReadXdcFromString(
        string content,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Xdc.ReadString(content, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <inheritdoc cref="XdcCanOpenOperations.ReadString"/>
    public static DeviceConfigurationFile ReadXdcFromString(string content, CanOpenFileOptions options)
        => Xdc.ReadString(content, options);

    /// <inheritdoc cref="XdcCanOpenOperations.ReadStream"/>
    public static DeviceConfigurationFile ReadXdc(
        Stream stream,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => Xdc.ReadStream(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize });

    /// <inheritdoc cref="XdcCanOpenOperations.ReadStream"/>
    public static DeviceConfigurationFile ReadXdc(Stream stream, CanOpenFileOptions options)
        => Xdc.ReadStream(stream, options);

    /// <inheritdoc cref="XdcCanOpenOperations.ReadStreamAsync"/>
    public static Task<DeviceConfigurationFile> ReadXdcAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => Xdc.ReadStreamAsync(stream, cancellationToken: cancellationToken);

    /// <inheritdoc cref="XdcCanOpenOperations.ReadStreamAsync"/>
    public static Task<DeviceConfigurationFile> ReadXdcAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
        => Xdc.ReadStreamAsync(stream, new CanOpenFileOptions { MaxInputSize = maxInputSize }, cancellationToken);

    /// <inheritdoc cref="XdcCanOpenOperations.ReadStreamAsync"/>
    public static Task<DeviceConfigurationFile> ReadXdcAsync(
        Stream stream,
        CanOpenFileOptions options,
        CancellationToken cancellationToken = default)
        => Xdc.ReadStreamAsync(stream, options, cancellationToken);

    #endregion


    #region XDC Write

        /// <inheritdoc cref="XdcCanOpenOperations.WriteFile(DeviceConfigurationFile, string)"/>


    [Obsolete("Use CanOpenFile.Xdc.WriteFile instead.")]
    public static void WriteXdc(DeviceConfigurationFile xdc, string filePath)
        => Xdc.WriteFile(xdc, filePath, options: null);

    /// <inheritdoc cref="XdcCanOpenOperations.WriteFile(DeviceConfigurationFile, string, CanOpenWriteOptions?)"/>
    public static void WriteXdc(DeviceConfigurationFile xdc, string filePath, CanOpenWriteOptions? options)
        => Xdc.WriteFile(xdc, filePath, options);

        /// <inheritdoc cref="XdcCanOpenOperations.WriteStream(DeviceConfigurationFile, Stream)"/>


    [Obsolete("Use CanOpenFile.Xdc.WriteStream instead.")]
    public static void WriteXdc(DeviceConfigurationFile xdc, Stream stream)
        => Xdc.WriteStream(xdc, stream);

    /// <inheritdoc cref="XdcCanOpenOperations.WriteStream(DeviceConfigurationFile, Stream, CanOpenWriteOptions?)"/>
    public static void WriteXdc(DeviceConfigurationFile xdc, Stream stream, CanOpenWriteOptions? options)
        => Xdc.WriteStream(xdc, stream, options);

        /// <inheritdoc cref="XdcCanOpenOperations.WriteFileAsync(DeviceConfigurationFile, string, CancellationToken)"/>


    [Obsolete("Use CanOpenFile.Xdc.WriteFileAsync instead.")]
    public static Task WriteXdcAsync(
        DeviceConfigurationFile xdc,
        string filePath,
        CancellationToken cancellationToken = default)
        => Xdc.WriteFileAsync(xdc, filePath, cancellationToken: cancellationToken);

    /// <inheritdoc cref="XdcCanOpenOperations.WriteFileAsync(DeviceConfigurationFile, string, CanOpenWriteOptions?, CancellationToken)"/>
    public static Task WriteXdcAsync(
        DeviceConfigurationFile xdc,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => Xdc.WriteFileAsync(xdc, filePath, options, cancellationToken);

        /// <inheritdoc cref="XdcCanOpenOperations.WriteStreamAsync(DeviceConfigurationFile, Stream, CancellationToken)"/>


    [Obsolete("Use CanOpenFile.Xdc.WriteStreamAsync instead.")]
    public static Task WriteXdcAsync(
        DeviceConfigurationFile xdc,
        Stream stream,
        CancellationToken cancellationToken = default)
        => Xdc.WriteStreamAsync(xdc, stream, cancellationToken: cancellationToken);

    /// <inheritdoc cref="XdcCanOpenOperations.WriteStreamAsync(DeviceConfigurationFile, Stream, CanOpenWriteOptions?, CancellationToken)"/>
    public static Task WriteXdcAsync(
        DeviceConfigurationFile xdc,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => Xdc.WriteStreamAsync(xdc, stream, options, cancellationToken);

        /// <inheritdoc cref="XdcCanOpenOperations.WriteToString(DeviceConfigurationFile)"/>


    [Obsolete("Use CanOpenFile.Xdc.WriteToString instead.")]
    public static string WriteXdcToString(DeviceConfigurationFile xdc)
        => Xdc.WriteToString(xdc, options: null);

    /// <inheritdoc cref="XdcCanOpenOperations.WriteToString(DeviceConfigurationFile, CanOpenWriteOptions?)"/>
    public static string WriteXdcToString(DeviceConfigurationFile xdc, CanOpenWriteOptions? options)
        => Xdc.WriteToString(xdc, options);

    #endregion

    #region EDS to DCF Conversion

    /// <inheritdoc cref="EdsCanOpenOperations.ConvertToDcf(ElectronicDataSheet, byte, ushort, string?)"/>
    [Obsolete("Use CanOpenFile.Eds.ConvertToDcf with an explicit timestamp for deterministic output.")]
    public static DeviceConfigurationFile EdsToDcf(
        ElectronicDataSheet eds,
        byte nodeId,
        ushort baudrate = 250,
        string? nodeName = null)
        => Eds.ConvertToDcf(eds, nodeId, baudrate, nodeName);

    /// <inheritdoc cref="EdsCanOpenOperations.ConvertToDcf(ElectronicDataSheet, byte, DateTime, ushort, string?)"/>
    public static DeviceConfigurationFile EdsToDcf(
        ElectronicDataSheet eds,
        byte nodeId,
        DateTime timestamp,
        ushort baudrate = 250,
        string? nodeName = null)
        => Eds.ConvertToDcf(eds, nodeId, timestamp, baudrate, nodeName);

    private static void ThrowIfInvalid(IReadOnlyList<ValidationIssue> issues)
    {
        if (issues.Count > 0)
            throw new ModelValidationException(issues);
    }

    #endregion
}
