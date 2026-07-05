namespace EdsDcfNet;

using EdsDcfNet.Exceptions;

/// <summary>
/// Shared read/write operations for a CiA CANopen file format model.
/// </summary>
/// <typeparam name="TModel">The in-memory model type for the format.</typeparam>
#pragma warning disable CA1822 // Instance API exposed via CanOpenFile format entry points.
public class FormatCanOpenOperations<TModel>
{
    /// <summary>
    /// Validates <paramref name="model"/> before a write operation when
    /// <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled.
    /// </summary>
    /// <param name="model">Model about to be written.</param>
    /// <param name="options">Write options; <see langword="null"/> means default (no validation).</param>
    protected delegate void EnsureValidForWriteCallback(TModel model, CanOpenWriteOptions? options);

    /// <summary>
    /// Validates <paramref name="model"/> asynchronously before a write operation when
    /// <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled.
    /// </summary>
    /// <param name="model">Model about to be written.</param>
    /// <param name="options">Write options; <see langword="null"/> means default (no validation).</param>
    /// <param name="cancellationToken">Cancellation token for aborting validation.</param>
    protected delegate Task EnsureValidForWriteAsyncCallback(
        TModel model,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads a model from a file on disk.
    /// </summary>
    /// <param name="filePath">Path to the input file.</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters.</param>
    protected delegate TModel ReadFileCallback(string filePath, long maxInputSize);

    /// <summary>
    /// Reads a model from a file on disk asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the input file.</param>
    /// <param name="maxInputSize">Maximum input size in bytes/characters.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O.</param>
    protected delegate Task<TModel> ReadFileAsyncCallback(string filePath, long maxInputSize, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a model from a string.
    /// </summary>
    /// <param name="content">File content as string.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters.</param>
    protected delegate TModel ReadStringCallback(string content, long maxInputSize);

    /// <summary>
    /// Reads a model from a stream. The stream is not disposed.
    /// </summary>
    /// <param name="stream">Readable input stream.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters.</param>
    protected delegate TModel ReadStreamCallback(Stream stream, long maxInputSize);

    /// <summary>
    /// Reads a model from a stream asynchronously. The stream is not disposed.
    /// </summary>
    /// <param name="stream">Readable input stream.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O.</param>
    protected delegate Task<TModel> ReadStreamAsyncCallback(Stream stream, long maxInputSize, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a model to a file on disk.
    /// </summary>
    /// <param name="model">Model to write.</param>
    /// <param name="filePath">Path to the output file.</param>
    protected delegate void WriteFileCallback(TModel model, string filePath);

    /// <summary>
    /// Writes a model to a stream. The stream is not disposed.
    /// </summary>
    /// <param name="model">Model to write.</param>
    /// <param name="stream">Writable output stream.</param>
    protected delegate void WriteStreamCallback(TModel model, Stream stream);

    /// <summary>
    /// Writes a model to a file on disk asynchronously.
    /// </summary>
    /// <param name="model">Model to write.</param>
    /// <param name="filePath">Path to the output file.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O.</param>
    protected delegate Task WriteFileAsyncCallback(TModel model, string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a model to a stream asynchronously. The stream is not disposed.
    /// </summary>
    /// <param name="model">Model to write.</param>
    /// <param name="stream">Writable output stream.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O.</param>
    protected delegate Task WriteStreamAsyncCallback(TModel model, Stream stream, CancellationToken cancellationToken);

    /// <summary>
    /// Serializes a model to a string.
    /// </summary>
    /// <param name="model">Model to serialize.</param>
    protected delegate string WriteToStringCallback(TModel model);

    // Felder als Action/Func (nicht delegate-Typen) - vermeidet Typkonflikte
    private readonly Action<TModel, CanOpenWriteOptions?> _ensureValidForWrite;
    private readonly Func<TModel, CanOpenWriteOptions?, CancellationToken, Task>? _ensureValidForWriteAsync;
    private readonly Func<string, long, TModel> _readFile;
    private readonly Func<string, long, CancellationToken, Task<TModel>> _readFileAsync;
    private readonly Func<string, long, TModel> _readString;
    private readonly Func<Stream, long, TModel> _readStream;
    private readonly Func<Stream, long, CancellationToken, Task<TModel>> _readStreamAsync;
    private readonly Action<TModel, string> _writeFile;
    private readonly Action<TModel, Stream> _writeStream;
    private readonly Func<TModel, string, CancellationToken, Task> _writeFileAsync;
    private readonly Func<TModel, Stream, CancellationToken, Task> _writeStreamAsync;
    private readonly Func<TModel, string> _writeToString;

    /// <summary>
    /// Initializes format-specific read/write delegates.
    /// </summary>
    /// <remarks>
    /// Parameter types remain <see cref="Action{T}"/>/<see cref="Func{TResult}"/> for
    /// binary compatibility with external subclasses. Named delegate types
    /// (<see cref="ReadFileCallback"/>, etc.) document the wiring contract.
    /// </remarks>
    protected FormatCanOpenOperations(
        Action<TModel, CanOpenWriteOptions?> ensureValidForWrite,
        Func<string, long, TModel> readFile,
        Func<string, long, CancellationToken, Task<TModel>> readFileAsync,
        Func<string, long, TModel> readString,
        Func<Stream, long, TModel> readStream,
        Func<Stream, long, CancellationToken, Task<TModel>> readStreamAsync,
        Action<TModel, string> writeFile,
        Action<TModel, Stream> writeStream,
        Func<TModel, string, CancellationToken, Task> writeFileAsync,
        Func<TModel, Stream, CancellationToken, Task> writeStreamAsync,
        Func<TModel, string> writeToString)
        : this(
            ensureValidForWrite,
            readFile,
            readFileAsync,
            readString,
            readStream,
            readStreamAsync,
            writeFile,
            writeStream,
            writeFileAsync,
            writeStreamAsync,
            writeToString,
            ensureValidForWriteAsync: null)
    {
    }

    /// <summary>
    /// Initializes format-specific read/write delegates, including an optional async validation delegate.
    /// </summary>
    protected FormatCanOpenOperations(
        Action<TModel, CanOpenWriteOptions?> ensureValidForWrite,
        Func<string, long, TModel> readFile,
        Func<string, long, CancellationToken, Task<TModel>> readFileAsync,
        Func<string, long, TModel> readString,
        Func<Stream, long, TModel> readStream,
        Func<Stream, long, CancellationToken, Task<TModel>> readStreamAsync,
        Action<TModel, string> writeFile,
        Action<TModel, Stream> writeStream,
        Func<TModel, string, CancellationToken, Task> writeFileAsync,
        Func<TModel, Stream, CancellationToken, Task> writeStreamAsync,
        Func<TModel, string> writeToString,
        Func<TModel, CanOpenWriteOptions?, CancellationToken, Task>? ensureValidForWriteAsync)
    {
        // Direkte Zuweisung - kein Wrapping, keine NullReferenceException
        _ensureValidForWrite = ensureValidForWrite;
        _ensureValidForWriteAsync = ensureValidForWriteAsync;
        _readFile = readFile;
        _readFileAsync = readFileAsync;
        _readString = readString;
        _readStream = readStream;
        _readStreamAsync = readStreamAsync;
        _writeFile = writeFile;
        _writeStream = writeStream;
        _writeFileAsync = writeFileAsync;
        _writeStreamAsync = writeStreamAsync;
        _writeToString = writeToString;
    }

    /// <summary>
    /// Reads a file from disk.
    /// </summary>
    public TModel ReadFile(string filePath, CanOpenFileOptions? options = null)
        => _readFile(filePath, CanOpenFileOptions.ResolveMaxInputSize(options));

    /// <summary>
    /// Reads a file from disk asynchronously.
    /// </summary>
    public Task<TModel> ReadFileAsync(
        string filePath,
        CanOpenFileOptions? options = null,
        CancellationToken cancellationToken = default)
        => _readFileAsync(filePath, CanOpenFileOptions.ResolveMaxInputSize(options), cancellationToken);

    /// <summary>
    /// Reads from a string.
    /// </summary>
    public TModel ReadString(string content, CanOpenFileOptions? options = null)
        => _readString(content, CanOpenFileOptions.ResolveMaxInputSize(options));

    /// <summary>
    /// Reads from a stream. The stream is not disposed.
    /// </summary>
    public TModel ReadStream(Stream stream, CanOpenFileOptions? options = null)
        => _readStream(stream, CanOpenFileOptions.ResolveMaxInputSize(options));

    /// <summary>
    /// Reads from a stream asynchronously. The stream is not disposed.
    /// </summary>
    public Task<TModel> ReadStreamAsync(
        Stream stream,
        CanOpenFileOptions? options = null,
        CancellationToken cancellationToken = default)
        => _readStreamAsync(stream, CanOpenFileOptions.ResolveMaxInputSize(options), cancellationToken);

    /// <summary>
    /// Writes to disk.
    /// </summary>
    public virtual void WriteFile(TModel model, string filePath)
        => WriteFile(model, filePath, options: null);

    /// <summary>
    /// Writes to disk.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public virtual void WriteFile(TModel model, string filePath, CanOpenWriteOptions? options)
    {
        _ensureValidForWrite(model, options);
        _writeFile(model, filePath);
    }

    /// <summary>
    /// Writes to a stream. The stream is not disposed.
    /// </summary>
    public virtual void WriteStream(TModel model, Stream stream)
        => WriteStream(model, stream, options: null);

    /// <summary>
    /// Writes to a stream. The stream is not disposed.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public virtual void WriteStream(TModel model, Stream stream, CanOpenWriteOptions? options)
    {
        _ensureValidForWrite(model, options);
        _writeStream(model, stream);
    }

    /// <summary>
    /// Writes to disk asynchronously.
    /// </summary>
    public virtual Task WriteFileAsync(
        TModel model,
        string filePath,
        CancellationToken cancellationToken = default)
        => WriteFileAsync(model, filePath, options: null, cancellationToken);

    /// <summary>
    /// Writes to disk asynchronously. When <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/>
    /// is enabled, validation also runs asynchronously and honors <paramref name="cancellationToken"/>. 
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public virtual async Task WriteFileAsync(
        TModel model,
        string filePath,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        await EnsureValidForWriteAsync(model, options, cancellationToken).ConfigureAwait(false);
        await _writeFileAsync(model, filePath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes to a stream asynchronously. The stream is not disposed.
    /// </summary>
    public virtual Task WriteStreamAsync(
        TModel model,
        Stream stream,
        CancellationToken cancellationToken = default)
        => WriteStreamAsync(model, stream, options: null, cancellationToken);

    /// <summary>
    /// Writes to a stream asynchronously. The stream is not disposed. When
    /// <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled, validation also
    /// runs asynchronously and honors <paramref name="cancellationToken"/>. 
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public virtual async Task WriteStreamAsync(
        TModel model,
        Stream stream,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        await EnsureValidForWriteAsync(model, options, cancellationToken).ConfigureAwait(false);
        await _writeStreamAsync(model, stream, cancellationToken).ConfigureAwait(false);
    }

    private Task EnsureValidForWriteAsync(
        TModel model,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken)
    {
        // Immer den Delegate aufrufen (wie in 1.9.x) – dieser entscheidet selbst,
        // ob validiert wird (z. B. via CanOpenWriteGuard.ShouldValidateBeforeWrite).
        if (_ensureValidForWriteAsync != null)
            return _ensureValidForWriteAsync(model, options, cancellationToken);

        // Fallback: synchronen Delegate asynchron ausführen
        cancellationToken.ThrowIfCancellationRequested();
        _ensureValidForWrite(model, options);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Serializes to a string.
    /// </summary>
    public virtual string WriteToString(TModel model)
        => WriteToString(model, options: null);

    /// <summary>
    /// Serializes to a string.
    /// </summary>
    /// <exception cref="ModelValidationException">
    /// Thrown when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled and the model has validation issues.
    /// </exception>
    public virtual string WriteToString(TModel model, CanOpenWriteOptions? options)
    {
        _ensureValidForWrite(model, options);
        return _writeToString(model);
    }
}
#pragma warning restore CA1822
