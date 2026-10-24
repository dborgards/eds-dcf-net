#if NETFRAMEWORK
namespace EdsDcfNet.Tests.Writers;

using System.Diagnostics;
using System.Reflection;
using EdsDcfNet.Utilities;

/// <summary>
/// Cancellation boundaries of the netstandard2.0 in-place fallback, executed by
/// the net48 host. Reflection reaches the private platform branch directly: a
/// regular Windows file otherwise uses the atomic replace path.
/// </summary>
public sealed class AtomicFileWriteNetStandardFallbackTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"atomic-fallback-{Guid.NewGuid():N}");

    public AtomicFileWriteNetStandardFallbackTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task WriteInPlaceAsync_CancelledBeforeTruncation_KeepsOldTarget()
    {
        // Arrange
        var target = Path.Combine(_dir, "before.eds");
        File.WriteAllText(target, "old");
        using var cancellation = new CancellationTokenSource();

        // Act
        var act = () => InvokeWriteInPlaceAsync(
            target,
            async stream =>
            {
                await stream.WriteAsync(new byte[] { 1, 2, 3 }, 0, 3);
                cancellation.Cancel();
            },
            cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        File.ReadAllText(target).Should().Be("old");
    }

    [Fact]
    public async Task WriteInPlaceAsync_CancelledAfterTruncation_FinishesCompleteContent()
    {
        // Arrange — the payload keeps the asynchronous copy active long enough to
        // observe FileMode.Create truncating the target before cancelling.
        var target = Path.Combine(_dir, "after.eds");
        File.WriteAllText(target, "old");
        var payload = new byte[16 * 1024 * 1024];
        new Random(42).NextBytes(payload);
        using var cancellation = new CancellationTokenSource();

        // Act
        var write = InvokeWriteInPlaceAsync(
            target,
            stream => stream.WriteAsync(payload, 0, payload.Length),
            cancellation.Token);
        var observedTruncation = false;
        var timeout = Stopwatch.StartNew();
        while (!write.IsCompleted && timeout.ElapsedMilliseconds < 10_000)
        {
            if (new FileInfo(target).Length == 0)
            {
                observedTruncation = true;
                cancellation.Cancel();
                break;
            }

            await Task.Yield();
        }

        // Assert
        observedTruncation.Should().BeTrue("the test must cancel after, not before, the destructive operation");
        await write;
        File.ReadAllBytes(target).Should().Equal(payload);
    }

    private static Task InvokeWriteInPlaceAsync(string target, Func<Stream, Task> write, CancellationToken cancellationToken)
    {
        var method = typeof(PlatformFileSupport).GetMethod(
            "WriteInPlaceAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("the netstandard2.0 fallback is part of the write contract");
        return (Task)method!.Invoke(null, new object[] { target, write, cancellationToken })!;
    }
}
#endif
