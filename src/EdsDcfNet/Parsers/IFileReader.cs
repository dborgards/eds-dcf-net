namespace EdsDcfNet.Parsers;

/// <summary>
/// Lightweight contract for readers that parse a file format into a strongly typed model.
/// </summary>
/// <typeparam name="TModel">Parsed model type returned by the reader.</typeparam>
public interface IFileReader<TModel>
{
    /// <summary>
    /// Reads content from a file path and parses it into <typeparamref name="TModel"/>.
    /// </summary>
    /// <param name="filePath">Path to the input file.</param>
    /// <param name="maxInputSize">Maximum file size in bytes.</param>
    /// <returns>Parsed model.</returns>
    TModel ReadFile(string filePath, long maxInputSize = IniParser.DefaultMaxInputSize);

    /// <summary>
    /// Reads content from a stream and parses it into <typeparamref name="TModel"/>.
    /// </summary>
    /// <param name="stream">Readable input stream.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters.</param>
    /// <returns>Parsed model.</returns>
    TModel ReadStream(Stream stream, long maxInputSize = IniParser.DefaultMaxInputSize);

    /// <summary>
    /// Reads content from a string and parses it into <typeparamref name="TModel"/>.
    /// </summary>
    /// <param name="content">Input content.</param>
    /// <param name="maxInputSize">Maximum content length in characters.</param>
    /// <returns>Parsed model.</returns>
    TModel ReadString(string content, long maxInputSize = IniParser.DefaultMaxInputSize);

    /// <summary>
    /// Asynchronously reads content from a file path and parses it into <typeparamref name="TModel"/>.
    /// </summary>
    /// <param name="filePath">Path to the input file.</param>
    /// <param name="maxInputSize">Maximum file size in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed model.</returns>
    Task<TModel> ReadFileAsync(
        string filePath,
        long maxInputSize = IniParser.DefaultMaxInputSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously reads content from a stream and parses it into <typeparamref name="TModel"/>.
    /// </summary>
    /// <param name="stream">Readable input stream.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed model.</returns>
    Task<TModel> ReadStreamAsync(
        Stream stream,
        long maxInputSize = IniParser.DefaultMaxInputSize,
        CancellationToken cancellationToken = default);
}
