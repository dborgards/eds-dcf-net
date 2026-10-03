namespace EdsDcfNet.Tests.Utilities;

using System.Diagnostics;
using AwesomeAssertions;
using Xunit;

public class AsyncCancellationTestSupportTests
{
    [Fact]
    public async Task AssertCanceledMidRunAsync_WhenDelegateRuns_ThrowsOperationCanceled()
    {
        var delegateRan = 0;

        await AsyncCancellationTestSupport.AssertCanceledMidRunAsync(
            cancellationToken => Task.Run(
                () =>
                {
                    Interlocked.Exchange(ref delegateRan, 1);
                    while (!cancellationToken.IsCancellationRequested)
                        Thread.Sleep(0);
                },
                cancellationToken));

        delegateRan.Should().Be(1);
    }

    [Fact(Timeout = 5000)]
    public async Task AssertCanceledMidRunAsync_WhenWorkNeverStarts_ThrowsTimeout()
    {
        // The fact timeout is a hang guard. xUnit v3 only signals this token; it does
        // not abort the test. The helper still throws its own TimeoutException when the
        // queued work never starts and that deadline is reached first.
        var hangGuard = TestContext.Current.CancellationToken;
        var delegateRan = 0;
        var scheduler = new QueuedOnlyScheduler();

        var act = () => AsyncCancellationTestSupport.AssertCanceledMidRunAsync(
            cancellationToken => Task.Factory.StartNew(
                () => Interlocked.Exchange(ref delegateRan, 1),
                cancellationToken,
                TaskCreationOptions.DenyChildAttach,
                scheduler),
            TimeSpan.FromMilliseconds(50),
            hangGuard);

        var assertion = act.Should().ThrowAsync<TimeoutException>();
        // Delay(-1) stays pending until its token is cancelled. Cancel the linked source
        // when the assertion finishes so the timer does not outlive this test.
        using var hangGuardWait = CancellationTokenSource.CreateLinkedTokenSource(hangGuard);
        var cancelWait = Task.Delay(-1, hangGuardWait.Token);
        var finished = await Task.WhenAny(assertion, cancelWait);
        hangGuardWait.Cancel();
        try
        {
            await cancelWait;
        }
        catch (OperationCanceledException)
        {
            // The hang-guard wait was released. A cancelled test token is reported below.
        }

        if (!ReferenceEquals(finished, assertion))
        {
            hangGuard.ThrowIfCancellationRequested();
        }

        await assertion;
        delegateRan.Should().Be(0);
    }

    [Fact]
    public async Task AssertCanceledMidRunAsync_WhenHangGuardCancelsBeforeWorkStarts_StopsWaiting()
    {
        var scheduler = new QueuedOnlyScheduler();
        using var hangGuard = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var started = Stopwatch.StartNew();

        var act = () => AsyncCancellationTestSupport.AssertCanceledMidRunAsync(
            cancellationToken => Task.Factory.StartNew(
                () => { },
                cancellationToken,
                TaskCreationOptions.DenyChildAttach,
                scheduler),
            TimeSpan.FromSeconds(30),
            hangGuard.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        started.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AssertCanceledMidRunAsync_WhenValidationAlwaysCompletes_ThrowsTimeout()
    {
        var act = () => AsyncCancellationTestSupport.AssertCanceledMidRunAsync(
            _ => Task.CompletedTask,
            TimeSpan.FromMilliseconds(50));

        await act.Should().ThrowAsync<TimeoutException>();
    }

    /// <summary>
    /// Queues tasks without executing them, keeping them in a pre-run state similar to a
    /// saturated thread pool where <see cref="Task.Run(Action, CancellationToken)"/> work
    /// has not yet started.
    /// </summary>
    private sealed class QueuedOnlyScheduler : TaskScheduler
    {
        private readonly List<Task> queuedTasks = [];

        protected override void QueueTask(Task task)
        {
            lock (queuedTasks)
                queuedTasks.Add(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
            => false;

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            lock (queuedTasks)
                return queuedTasks.ToArray();
        }
    }
}
