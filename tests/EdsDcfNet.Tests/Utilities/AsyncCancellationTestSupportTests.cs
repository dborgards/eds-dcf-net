namespace EdsDcfNet.Tests.Utilities;

using FluentAssertions;
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
        var delegateRan = 0;
        var scheduler = new QueuedOnlyScheduler();

        var act = () => AsyncCancellationTestSupport.AssertCanceledMidRunAsync(
            cancellationToken => Task.Factory.StartNew(
                () => Interlocked.Exchange(ref delegateRan, 1),
                cancellationToken,
                TaskCreationOptions.DenyChildAttach,
                scheduler),
            TimeSpan.FromMilliseconds(50));

        await act.Should().ThrowAsync<TimeoutException>();
        delegateRan.Should().Be(0);
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
