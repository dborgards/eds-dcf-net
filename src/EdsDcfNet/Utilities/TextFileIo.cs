namespace EdsDcfNet.Utilities;

using System.Text;
using EdsDcfNet.Parsers;
#if !NET10_0_OR_GREATER
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
#endif

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
    {
        filePath = ResolveWriteTarget(filePath);
#if !NET10_0_OR_GREATER
        if (MustWriteInPlace(filePath))
        {
            WriteInPlace(filePath, write);
            return;
        }
#endif

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
                mode = CopyUnixFileMode(filePath, tempPath);
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            ReapplyUnixFileMode(tempPath, mode);
            Commit(tempPath, filePath);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    /// <summary>Asynchronous variant of <see cref="WriteFileAtomic"/>; cancellation before the commit removes the temporary file.</summary>
    internal static async Task WriteFileAtomicAsync(
        string filePath,
        Func<Stream, Task> write,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        filePath = ResolveWriteTarget(filePath);
#if !NET10_0_OR_GREATER
        if (MustWriteInPlace(filePath))
        {
            await WriteInPlaceAsync(filePath, write, cancellationToken).ConfigureAwait(false);
            return;
        }
#endif

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
                mode = CopyUnixFileMode(filePath, tempPath);
                await write(stream).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            ReapplyUnixFileMode(tempPath, mode);
            Commit(tempPath, filePath);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    /// <summary>
    /// Absolute path of the file that receives the content. On net10.0 a symbolic link is
    /// resolved to its final target, so the commit replaces that file and not the link.
    /// </summary>
    private static string ResolveWriteTarget(string filePath)
    {
        filePath = Path.GetFullPath(filePath);
#if NET10_0_OR_GREATER
        var info = new FileInfo(filePath);
        if (info.LinkTarget == null)
            return filePath;

        return info.ResolveLinkTarget(returnFinalTarget: true)!.FullName;
#else
        return filePath;
#endif
    }

#if !NET10_0_OR_GREATER
    /// <summary>
    /// netstandard2.0 cannot resolve a link target, so a reparse point is written in place
    /// through the link instead of being replaced. On a Unix runtime without
    /// <c>File.SetUnixFileMode</c> an existing file is written in place as well, so it keeps its mode.
    /// </summary>
    private static bool MustWriteInPlace(string filePath)
        => IsReparsePoint(filePath)
           || (!IsWindows && SetUnixFileModeMethod == null && File.Exists(filePath));

    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    // File.GetUnixFileMode/SetUnixFileMode exist from .NET 7 on, which also runs this build.
    // Looked up by reflection: the Polyfill substitute shells out to stat/chmod.
    private static readonly MethodInfo? GetUnixFileModeMethod
        = typeof(File).GetMethod("GetUnixFileMode", new[] { typeof(string) });

    private static readonly MethodInfo? SetUnixFileModeMethod = GetUnixFileModeMethod == null
        ? null
        : typeof(File).GetMethod("SetUnixFileMode", new[] { typeof(string), GetUnixFileModeMethod.ReturnType });

    private static object? InvokeStatic(MethodInfo method, params object?[] arguments)
    {
        try
        {
            return method.Invoke(null, arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static bool IsReparsePoint(string filePath)
    {
        try
        {
            return (File.GetAttributes(filePath) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// netstandard2.0 fallback: serializes into memory first, then overwrites the file the path
    /// refers to (following a link) in place. See <see cref="WriteFileAtomic"/>.
    /// </summary>
    private static void WriteInPlace(string filePath, Action<Stream> write)
    {
        using var buffer = new MemoryStream();
        write(buffer);

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096);
        buffer.WriteTo(stream);
        stream.Flush(flushToDisk: true);
    }

    private static async Task WriteInPlaceAsync(string filePath, Func<Stream, Task> write, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await write(buffer).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        using var stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            options: FileOptions.Asynchronous);
        buffer.Position = 0;
        await buffer.CopyToAsync(stream, 4096, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }
#endif

    /// <summary>
    /// Gives the temporary file the Unix permission bits of the file it will replace, before any
    /// content is written. <see cref="File.Replace(string, string, string?)"/> renames the
    /// temporary file over the target on Unix, so without this the new file would receive the
    /// umask default (typically 0644) and widen a restrictive mode such as 0600. No-op on Windows,
    /// where the replace keeps the target's attributes and ACL, and when the target does not exist.
    /// Returns the captured mode (boxed <c>UnixFileMode</c>) for <see cref="ReapplyUnixFileMode"/>,
    /// or <see langword="null"/> when nothing was copied.
    /// </summary>
    private static object? CopyUnixFileMode(string filePath, string tempPath)
    {
#if NET10_0_OR_GREATER
        if (OperatingSystem.IsWindows() || !File.Exists(filePath))
            return null;

        var mode = File.GetUnixFileMode(filePath);
        File.SetUnixFileMode(tempPath, mode);
        return mode;
#else
        if (IsWindows || SetUnixFileModeMethod == null || !File.Exists(filePath))
            return null;

        var mode = InvokeStatic(GetUnixFileModeMethod!, filePath);
        InvokeStatic(SetUnixFileModeMethod, tempPath, mode);
        return mode;
#endif
    }

    /// <summary>
    /// Applies the captured mode again after the content is written and flushed, immediately
    /// before the commit: writing to a file clears set-user-ID and set-group-ID on Linux when the
    /// process lacks <c>CAP_FSETID</c>, so the early copy alone would drop those bits.
    /// </summary>
    private static void ReapplyUnixFileMode(string tempPath, object? mode)
    {
        if (mode == null)
            return;

#if NET10_0_OR_GREATER
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(tempPath, (UnixFileMode)mode);
#else
        InvokeStatic(SetUnixFileModeMethod!, tempPath, mode);
#endif
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
