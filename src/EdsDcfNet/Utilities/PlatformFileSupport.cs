namespace EdsDcfNet.Utilities;

using System.Diagnostics.CodeAnalysis;
#if !NET10_0_OR_GREATER
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Platform-specific file operations behind <see cref="TextFileIo.WriteFileAtomic"/>: symbolic
/// link resolution, Unix permission bits, and the netstandard2.0 in-place fallback. The
/// platform-neutral commit sequence (temporary file, write, flush, replace, cleanup) stays in
/// <see cref="TextFileIo"/> and is measured everywhere.
/// </summary>
/// <remarks>
/// Excluded from coverage measurement because these branches execute only on Unix or only on the
/// netstandard2.0 asset, while CI measures coverage on windows-latest. They are covered by
/// <c>AtomicFileWriteTargetTests</c> on Unix (symbolic links, 0600/0640, set-user-ID and
/// set-group-ID) and were verified manually for the netstandard2.0 asset on a .NET 10 runtime.
/// </remarks>
#if NET10_0_OR_GREATER
[ExcludeFromCodeCoverage(Justification = "Executes only on Unix / only on the netstandard2.0 asset; CI measures coverage on windows-latest. Covered by AtomicFileWriteTargetTests on Unix.")]
#else
// netstandard2.0 has no Justification property; see the remarks above.
[ExcludeFromCodeCoverage]
#endif
internal static class PlatformFileSupport
{
    /// <summary>
    /// Final target of <paramref name="fullPath"/> when it is a symbolic link (through any chain,
    /// also a dangling one); otherwise <paramref name="fullPath"/>. The netstandard2.0 build cannot
    /// resolve links and returns the path unchanged; <see cref="Write"/> handles links there.
    /// </summary>
    internal static string ResolveLinkTarget(string fullPath)
    {
#if NET10_0_OR_GREATER
        // Walks the raw link text instead of ResolveLinkTarget(returnFinalTarget: true), which
        // rewrites a UNC target into "<cwd>\UNC\server\share\..." on Windows.
        var current = fullPath;
        for (var depth = 0; depth <= SymbolicLinkPath.MaxDepth; depth++)
        {
            var rawTarget = new FileInfo(current).LinkTarget;
            if (rawTarget == null)
                return current;

            current = SymbolicLinkPath.Next(current, rawTarget);
        }

        throw new IOException("Too many levels of symbolic links: " + fullPath);
#else
        return fullPath;
#endif
    }

    /// <summary>
    /// Runs <paramref name="replace"/> (the atomic commit), except on netstandard2.0 when the target
    /// must be written in place: a reparse point (the link target cannot be resolved), or an
    /// existing file on a Unix runtime without <c>File.SetUnixFileMode</c> (so it keeps its mode).
    /// The in-place write serializes into memory first, so a serialization failure leaves the
    /// target untouched.
    /// </summary>
    internal static void Write(string filePath, Action<Stream> write, Action<string, Action<Stream>> replace)
    {
#if !NET10_0_OR_GREATER
        if (MustWriteInPlace(filePath))
        {
            WriteInPlace(filePath, write);
            return;
        }
#endif
        replace(filePath, write);
    }

    /// <summary>Asynchronous variant of <see cref="Write"/>.</summary>
    internal static Task WriteAsync(
        string filePath,
        Func<Stream, Task> write,
        Func<string, Func<Stream, Task>, CancellationToken, Task> replace,
        CancellationToken cancellationToken)
    {
#if !NET10_0_OR_GREATER
        if (MustWriteInPlace(filePath))
            return WriteInPlaceAsync(filePath, write, cancellationToken);
#endif
        return replace(filePath, write, cancellationToken);
    }

    /// <summary>
    /// Gives the temporary file the Unix permission bits of the file it will replace, before any
    /// content is written. <see cref="File.Replace(string, string, string?)"/> renames the
    /// temporary file over the target on Unix, so without this the new file would receive the
    /// umask default (typically 0644) and widen a restrictive mode such as 0600. No-op on Windows,
    /// where the replace keeps the target's attributes and ACL, and when the target does not exist.
    /// Returns the captured mode (boxed <c>UnixFileMode</c>) for <see cref="ReapplyUnixFileMode"/>,
    /// or <see langword="null"/> when nothing was copied.
    /// </summary>
    internal static object? CopyUnixFileMode(string filePath, string tempPath)
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
    internal static void ReapplyUnixFileMode(string tempPath, object? mode)
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

#if !NET10_0_OR_GREATER
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    // File.GetUnixFileMode/SetUnixFileMode exist from .NET 7 on, which also runs this build.
    // Looked up by reflection: the Polyfill substitute shells out to stat/chmod.
    private static readonly MethodInfo? GetUnixFileModeMethod
        = typeof(File).GetMethod("GetUnixFileMode", new[] { typeof(string) });

    private static readonly MethodInfo? SetUnixFileModeMethod = GetUnixFileModeMethod == null
        ? null
        : typeof(File).GetMethod("SetUnixFileMode", new[] { typeof(string), GetUnixFileModeMethod.ReturnType });

    private static bool MustWriteInPlace(string filePath)
        => IsReparsePoint(filePath)
           || (!IsWindows && SetUnixFileModeMethod == null && File.Exists(filePath));

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
}
