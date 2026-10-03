namespace EdsDcfNet.Utilities;

using System.Text;
using EdsDcfNet.Parsers;

internal static class TextFileIo
{
    internal static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// The single place that decides the encoding of every byte the writers produce
    /// (EDS, DCF, CPJ, XDD, XDC; file and stream, sync and async).
    /// <see cref="FileEncodingScope.CurrentWrite"/> null keeps UTF-8 without a BOM.
    /// An explicit encoding is cloned with <see cref="EncoderFallback.ExceptionFallback"/>
    /// so INI output throws instead of substituting '?'. The XML writers pass the same
    /// instance to <see cref="System.Xml.XmlWriterSettings.Encoding"/>: the <c>XmlWriter</c>
    /// emits the matching declaration and writes numeric character references for text and
    /// attribute characters the encoding cannot represent. Characters that cannot be
    /// referenced (for example comments) hit the exception fallback.
    /// </summary>
    internal static Encoding GetOutputEncoding()
    {
        var requested = FileEncodingScope.CurrentWrite;
        if (requested == null)
            return Utf8NoBom;

        var clone = (Encoding)requested.Clone();
        clone.EncoderFallback = EncoderFallback.ExceptionFallback;
        return clone;
    }

    /// <summary>
    /// The single place where INI output (EDS, DCF, CPJ) receives its line ending. The writers
    /// build text with <see cref="StringBuilder.AppendLine(string)"/> and pass it through here
    /// just before returning it. When no <see cref="CanOpenWriteOptions.NewLine"/> was chosen the
    /// text is returned unchanged; otherwise every line break (CRLF or LF) becomes the
    /// chosen one. The INI write rules reject values that contain line breaks, so only the
    /// writers' own line terminators (CRLF or LF) occur.
    /// </summary>
    internal static string ApplyOutputNewLine(string text)
    {
        var newLine = FileEncodingScope.CurrentWriteNewLine;
        if (newLine == null)
            return text;

        return text.Replace("\r\n", "\n").Replace("\n", newLine);
    }

    /// <summary>Writes <paramref name="content"/> to <paramref name="stream"/> using <see cref="GetOutputEncoding"/>.</summary>
    internal static void WriteOutputText(Stream stream, string content)
        => WriteAllText(stream, content, GetOutputEncoding(), leaveOpen: true);

    /// <summary>Asynchronously writes <paramref name="content"/> to <paramref name="stream"/> using <see cref="GetOutputEncoding"/>.</summary>
    internal static Task WriteOutputTextAsync(Stream stream, string content, CancellationToken cancellationToken)
        => WriteAllTextAsync(stream, content, GetOutputEncoding(), leaveOpen: true, cancellationToken: cancellationToken);

    /// <summary>Atomically writes <paramref name="content"/> to <paramref name="filePath"/> using <see cref="GetOutputEncoding"/>.</summary>
    internal static void WriteOutputTextToFile(string filePath, string content)
        => WriteFileAtomic(filePath, stream => WriteOutputText(stream, content));

    /// <summary>Asynchronously and atomically writes <paramref name="content"/> to <paramref name="filePath"/> using <see cref="GetOutputEncoding"/>.</summary>
    internal static Task WriteOutputTextToFileAsync(string filePath, string content, CancellationToken cancellationToken)
        => WriteFileAtomicAsync(filePath, stream => WriteOutputTextAsync(stream, content, cancellationToken), cancellationToken);

    /// <summary>
    /// Writes a file through a temporary file in the target directory (same volume) and commits it
    /// with <see cref="File.Move(string, string)"/> (target absent) or
    /// <see cref="File.Replace(string, string, string?)"/> (target present). The previous target
    /// is never deleted before the new content is complete; on any failure the temporary file is
    /// removed and the target stays untouched. There is no fallback to in-place overwriting,
    /// with one exception on <c>netstandard2.0</c> described below.
    /// </summary>
    /// <remarks>
    /// A symbolic link is followed: the final target (through any chain of links, also a
    /// dangling one) is replaced and the link stays a link. The temporary file is created in the
    /// final target's directory. On <c>netstandard2.0</c> the link target cannot be resolved;
    /// a path that is a reparse point (symbolic link, junction, and other reparse points on
    /// Windows) is therefore written in place through the link: the content is first
    /// serialized completely into memory, so a serialization failure leaves the target
    /// untouched, but an I/O failure while writing can leave it truncated.
    /// On Unix the replacing file receives the permission bits of the file it replaces. The
    /// netstandard2.0 build reads them through <c>File.GetUnixFileMode</c> when the runtime
    /// provides it (.NET 7 and later); on older Unix runtimes it writes an existing file in place
    /// in the same way, which keeps its mode.
    /// </remarks>
    internal static void WriteFileAtomic(string filePath, Action<Stream> write)
        => PlatformFileSupport.Write(ResolveWriteTarget(filePath), write, ReplaceThroughTempFile);

    /// <summary>Asynchronous variant of <see cref="WriteFileAtomic"/>; cancellation before the commit removes the temporary file.</summary>
    internal static async Task WriteFileAtomicAsync(
        string filePath,
        Func<Stream, Task> write,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await PlatformFileSupport.WriteAsync(ResolveWriteTarget(filePath), write, ReplaceThroughTempFileAsync, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Absolute path of the file that receives the content; a symbolic link is resolved to its
    /// final target (net10.0), so the commit replaces that file and not the link.
    /// </summary>
    private static string ResolveWriteTarget(string filePath)
        => PlatformFileSupport.ResolveLinkTarget(Path.GetFullPath(filePath));

    /// <summary>The platform-neutral commit sequence of <see cref="WriteFileAtomic"/>.</summary>
    private static void ReplaceThroughTempFile(string filePath, Action<Stream> write)
    {
        var tempPath = CreateTempPath(filePath);
        try
        {
            object? mode;
            using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096))
            {
                mode = PlatformFileSupport.CopyUnixFileMode(filePath, tempPath);
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            PlatformFileSupport.ReapplyUnixFileMode(tempPath, mode);
            Commit(tempPath, filePath);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    /// <summary>The platform-neutral commit sequence of <see cref="WriteFileAtomicAsync"/>.</summary>
    private static async Task ReplaceThroughTempFileAsync(
        string filePath,
        Func<Stream, Task> write,
        CancellationToken cancellationToken)
    {
        var tempPath = CreateTempPath(filePath);
        try
        {
            object? mode;
            using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                options: FileOptions.Asynchronous))
            {
                mode = PlatformFileSupport.CopyUnixFileMode(filePath, tempPath);
                await write(stream).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            PlatformFileSupport.ReapplyUnixFileMode(tempPath, mode);
            Commit(tempPath, filePath);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static string CreateTempPath(string filePath)
    {
        // filePath is already absolute (resolved once by the caller), so temp placement and commit agree.
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
            throw new ArgumentException("File path must include a file name.", nameof(filePath));

        // Independent of the target file name. Embedding that name plus a GUID and ".tmp"
        // (38 extra characters) exceeds the per-component limit — 255 bytes on ext4, 255
        // characters on NTFS — when the target name is already near it, so every writer
        // would reject a path that was previously writable. ".edsdcf." + 32 hex digits + ".tmp"
        // is 44 ASCII characters and still sits in the target directory (same volume).
        return Path.Combine(directory, $".edsdcf.{Guid.NewGuid():N}.tmp");
    }

    private static void Commit(string tempPath, string filePath)
    {
        if (File.Exists(filePath))
            File.Replace(tempPath, filePath, destinationBackupFileName: null);
        else
            File.Move(tempPath, filePath);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: the original failure is the one worth reporting.
        }
    }

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

    internal static void WriteAllText(
        Stream stream,
        string content,
        Encoding encoding,
        bool leaveOpen = true)
    {
        ThrowIfNull(stream, nameof(stream));
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
        ThrowIfNull(stream, nameof(stream));
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

    private static void ThrowIfNull(object? value, string parameterName)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value, parameterName);
#else
        if (value == null)
            throw new ArgumentNullException(parameterName);
#endif
    }
}
