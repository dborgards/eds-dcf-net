namespace EdsDcfNet.Utilities;

using System.Text;

internal static class TextFileIo
{
    internal static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    internal static async Task<string> ReadAllTextAsync(
        string filePath,
        Encoding encoding,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);

#if NET10_0_OR_GREATER
        var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
#else
        var content = await reader.ReadToEndAsync().ConfigureAwait(false);
#endif
        cancellationToken.ThrowIfCancellationRequested();
        return content;
    }

    internal static async Task WriteAllTextAsync(
        string filePath,
        string content,
        Encoding encoding,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            options: FileOptions.Asynchronous);
        using var writer = new StreamWriter(stream, encoding);

#if NET10_0_OR_GREATER
        await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
#else
        await writer.WriteAsync(content).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
#endif
        cancellationToken.ThrowIfCancellationRequested();
    }
}
