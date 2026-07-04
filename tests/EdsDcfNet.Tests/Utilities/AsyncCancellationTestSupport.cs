namespace EdsDcfNet.Tests.Utilities;

using System.Diagnostics;

/// <summary>
/// Helpers for async cancellation tests that must cancel while validation work is in progress.
/// </summary>
internal static class AsyncCancellationTestSupport
{
    /// <summary>
    /// Repeatedly starts <paramref name="validateAsync"/> and cancels it once the returned task
    /// has begun executing, asserting that a run observes the cancellation and throws
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <remarks>
    /// A single start-then-cancel attempt is inherently racy: a fast validation can run to
    /// completion before <see cref="CancellationTokenSource.Cancel()"/> is applied, in which
    /// case no <see cref="OperationCanceledException"/> is thrown. Rather than let that race
    /// fail the test, each attempt uses a fresh <see cref="CancellationTokenSource"/> (a
    /// cancelled source cannot be reused) and the helper retries until a run is cancelled
    /// mid-flight. This keeps the test reliable regardless of thread-pool scheduling while still
    /// exercising the in-loop cancellation checkpoints: cancellation is applied only after the
    /// delegate has started running, not at scheduling time.
    /// </remarks>
    /// <param name="validateAsync">
    /// Factory that starts the validation with the supplied token and returns its task.
    /// </param>
    /// <param name="timeout">
    /// Maximum time to spend attempting to observe mid-run cancellation. Defaults to 30 seconds.
    /// </param>
    /// <exception cref="TimeoutException">
    /// Thrown when no attempt observes mid-run cancellation within <paramref name="timeout"/>.
    /// </exception>
    public static async Task AssertCanceledMidRunAsync(
        Func<CancellationToken, Task> validateAsync,
        TimeSpan? timeout = null)
    {
        var deadline = timeout ?? TimeSpan.FromSeconds(30);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < deadline)
        {
            using var cts = new CancellationTokenSource();
            var task = validateAsync(cts.Token);

            // Wait until the delegate has actually started (or already finished) so that
            // cancellation, when it lands, is observed at an in-loop checkpoint rather than
            // at task scheduling.
            while ((task.Status == TaskStatus.WaitingToRun ||
                    task.Status == TaskStatus.WaitingForActivation) &&
                   stopwatch.Elapsed < deadline)
            {
                await Task.Yield();
            }

            if (task.Status != TaskStatus.Running)
            {
                // Validation finished first, is still queued, or we ran out of time waiting
                // for a worker. Do not Cancel()+await here: Task.Run(..., token) reports
                // scheduling-time cancellation as OperationCanceledException even though the
                // delegate never ran.
                continue;
            }

            cts.Cancel();

            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Validation completed before cancellation was observed; try again with a fresh
            // token source until we win the race or run out of time.
        }

        throw new TimeoutException(
            "Validation never observed mid-run cancellation within the timeout.");
    }
}
