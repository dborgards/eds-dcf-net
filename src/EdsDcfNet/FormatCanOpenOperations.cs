namespace EdsDcfNet;

using EdsDcfNet.Exceptions;

/// <summary>
/// Shared read/write operations for a CiA CANopen file format model.
/// </summary>
/// <typeparam name="TModel">The in-memory model type for the format.</typeparam>
#pragma warning disable CA1822 // Instance API exposed via CanOpenFile format entry points.
public class FormatCanOpenOperations<TModel>
{
    // Fields stay Action/Func (no named delegate types): the constructors accept
    // Action/Func, and wrapping them in named delegate types broke construction
    // for null delegates from 1.9.x subclasses (#358).
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
    /// binary compatibility with external subclasses; each parameter's wiring
    /// contract is documented below.
    /// </remarks>
    /// <param name="ensureValidForWrite">Validates <c>(model, options)</c> before a write when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled; <see langword="null"/> options mean default (no validation).</param>
    /// <param name="readFile">Reads a model from <c>(filePath, maxInputSize)</c>; size in bytes.</param>
    /// <param name="readFileAsync">Reads a model from <c>(filePath, maxInputSize, cancellationToken)</c> asynchronously.</param>
    /// <param name="readString">Reads a model from <c>(content, maxInputSize)</c>; size is the maximum decoded content length in characters.</param>
    /// <param name="readStream">Reads a model from <c>(stream, maxInputSize)</c>; the stream is not disposed.</param>
    /// <param name="readStreamAsync">Reads a model from <c>(stream, maxInputSize, cancellationToken)</c> asynchronously; the stream is not disposed.</param>
    /// <param name="writeFile">Writes <c>(model, filePath)</c> to disk.</param>
    /// <param name="writeStream">Writes <c>(model, stream)</c>; the stream is not disposed.</param>
    /// <param name="writeFileAsync">Writes <c>(model, filePath, cancellationToken)</c> to disk asynchronously.</param>
    /// <param name="writeStreamAsync">Writes <c>(model, stream, cancellationToken)</c> asynchronously; the stream is not disposed.</param>
    /// <param name="writeToString">Serializes <c>(model)</c> to a string.</param>
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
    /// <remarks>
    /// See the eleven-parameter constructor for the wiring contract of the shared parameters.
    /// </remarks>
    /// <param name="ensureValidForWrite">Validates <c>(model, options)</c> before a write when <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/> is enabled.</param>
    /// <param name="readFile">Reads a model from <c>(filePath, maxInputSize)</c>.</param>
    /// <param name="readFileAsync">Reads a model from <c>(filePath, maxInputSize, cancellationToken)</c> asynchronously.</param>
    /// <param name="readString">Reads a model from <c>(content, maxInputSize)</c>.</param>
    /// <param name="readStream">Reads a model from <c>(stream, maxInputSize)</c>; the stream is not disposed.</param>
    /// <param name="readStreamAsync">Reads a model from <c>(stream, maxInputSize, cancellationToken)</c> asynchronously; the stream is not disposed.</param>
    /// <param name="writeFile">Writes <c>(model, filePath)</c> to disk.</param>
    /// <param name="writeStream">Writes <c>(model, stream)</c>; the stream is not disposed.</param>
    /// <param name="writeFileAsync">Writes <c>(model, filePath, cancellationToken)</c> to disk asynchronously.</param>
    /// <param name="writeStreamAsync">Writes <c>(model, stream, cancellationToken)</c> asynchronously; the stream is not disposed.</param>
    /// <param name="writeToString">Serializes <c>(model)</c> to a string.</param>
    /// <param name="ensureValidForWriteAsync">Optional async counterpart of <paramref name="ensureValidForWrite"/> taking <c>(model, options, cancellationToken)</c>; when <see langword="null"/>, validated async writes fall back to the synchronous delegate.</param>
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
