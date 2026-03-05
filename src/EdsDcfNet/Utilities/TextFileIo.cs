namespace EdsDcfNet.Utilities;

using System.Text;

internal static class TextFileIo
{
    internal static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    internal static async Task<string> ReadAllTextAsync(
        string filePath,
        Encoding encoding,
        bool detectEncodingFromByteOrderMarks = true,
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
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks);

#if NET10_0_OR_GREATER
        var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
#else
        var builder = new StringBuilder();
        var buffer = new char[4096];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            if (charsRead == 0)
                break;

            builder.Append(buffer, 0, charsRead);
        }
        var content = builder.ToString();
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
        const int chunkSize = 4096;
        var buffer = new char[chunkSize];
        for (var offset = 0; offset < content.Length; offset += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var length = Math.Min(chunkSize, content.Length - offset);
            content.CopyTo(offset, buffer, 0, length);
            await writer.WriteAsync(buffer, 0, length).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await writer.FlushAsync().ConfigureAwait(false);
#endif
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static string ReadAllText(
        Stream stream,
        Encoding encoding,
        bool detectEncodingFromByteOrderMarks = true,
        bool leaveOpen = true)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));

        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks, bufferSize: 4096, leaveOpen: leaveOpen);
        return reader.ReadToEnd();
    }

    internal static async Task<string> ReadAllTextAsync(
        Stream stream,
        Encoding encoding,
        bool detectEncodingFromByteOrderMarks = true,
        bool leaveOpen = true,
        CancellationToken cancellationToken = default)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));

        cancellationToken.ThrowIfCancellationRequested();
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks, bufferSize: 4096, leaveOpen: leaveOpen);

#if NET10_0_OR_GREATER
        var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
#else
        var builder = new StringBuilder();
        var buffer = new char[4096];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            if (charsRead == 0)
                break;

            builder.Append(buffer, 0, charsRead);
        }
        var content = builder.ToString();
#endif
        cancellationToken.ThrowIfCancellationRequested();
        return content;
    }

    internal static void WriteAllText(
        Stream stream,
        string content,
        Encoding encoding,
        bool leaveOpen = true)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanWrite) throw new ArgumentException("Stream must be writable.", nameof(stream));

        using var writer = new StreamWriter(stream, encoding, bufferSize: 4096, leaveOpen: leaveOpen);
        writer.Write(content);
        writer.Flush();
    }

    internal static async Task WriteAllTextAsync(
        Stream stream,
        string content,
        Encoding encoding,
        bool leaveOpen = true,
        CancellationToken cancellationToken = default)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanWrite) throw new ArgumentException("Stream must be writable.", nameof(stream));

        cancellationToken.ThrowIfCancellationRequested();
        using var writer = new StreamWriter(stream, encoding, bufferSize: 4096, leaveOpen: leaveOpen);

#if NET10_0_OR_GREATER
        await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
#else
        const int chunkSize = 4096;
        var buffer = new char[chunkSize];
        for (var offset = 0; offset < content.Length; offset += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var length = Math.Min(chunkSize, content.Length - offset);
            content.CopyTo(offset, buffer, 0, length);
            await writer.WriteAsync(buffer, 0, length).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await writer.FlushAsync().ConfigureAwait(false);
#endif
        cancellationToken.ThrowIfCancellationRequested();
    }
}
