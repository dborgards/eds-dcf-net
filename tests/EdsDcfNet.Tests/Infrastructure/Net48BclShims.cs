#if NETFRAMEWORK
namespace EdsDcfNet.Tests.Infrastructure;

using System.Diagnostics;
using System.Text;
using System.Threading;

/// <summary>
/// Minimal BCL shims so the test suite can target net48 without Polyfill
/// (Polyfill 10.x double-includes contentFiles for the net48 TFM).
/// </summary>
internal static class Net48BclShims
{
    extension(File)
    {
        public static Task WriteAllTextAsync(string path, string? contents, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllText(path, contents);
            return Task.CompletedTask;
        }

        public static Task WriteAllTextAsync(string path, string? contents, Encoding encoding, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllText(path, contents, encoding);
            return Task.CompletedTask;
        }

        public static Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(File.ReadAllBytes(path));
        }
    }

    extension(Path)
    {
        public static string TrimEndingDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            var length = path.Length;
            if (length > 1 && (path[length - 1] == Path.DirectorySeparatorChar || path[length - 1] == Path.AltDirectorySeparatorChar))
            {
                return path.Substring(0, length - 1);
            }

            return path;
        }
    }

    extension(Process process)
    {
        public Task WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            if (process.HasExited)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnExited(object? sender, EventArgs e)
            {
                process.Exited -= OnExited;
                tcs.TrySetResult(null);
            }

            process.EnableRaisingEvents = true;
            process.Exited += OnExited;

            if (process.HasExited)
            {
                process.Exited -= OnExited;
                return Task.CompletedTask;
            }

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(
                    static state =>
                    {
                        var (source, targetProcess, handler) = ((TaskCompletionSource<object?>, Process, EventHandler))state!;
                        targetProcess.Exited -= handler;
                        source.TrySetCanceled();
                    },
                    (tcs, process, (EventHandler)OnExited));
            }

            return tcs.Task;
        }
    }

    extension(Task task)
    {
        public bool IsCompletedSuccessfully => task.Status == TaskStatus.RanToCompletion;
    }

    extension(string value)
    {
        public int GetHashCode(StringComparison comparisonType)
        {
            return comparisonType switch
            {
                StringComparison.CurrentCulture => StringComparer.CurrentCulture.GetHashCode(value),
                StringComparison.CurrentCultureIgnoreCase => StringComparer.CurrentCultureIgnoreCase.GetHashCode(value),
                StringComparison.InvariantCulture => StringComparer.InvariantCulture.GetHashCode(value),
                StringComparison.InvariantCultureIgnoreCase => StringComparer.InvariantCultureIgnoreCase.GetHashCode(value),
                StringComparison.Ordinal => StringComparer.Ordinal.GetHashCode(value),
                StringComparison.OrdinalIgnoreCase => StringComparer.OrdinalIgnoreCase.GetHashCode(value),
                _ => throw new ArgumentOutOfRangeException(nameof(comparisonType)),
            };
        }
    }
}
#endif
