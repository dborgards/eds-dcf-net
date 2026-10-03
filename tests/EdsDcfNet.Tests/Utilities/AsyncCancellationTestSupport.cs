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
    /// <param name="cancellationToken">
    /// Aborts this wait when cancelled. A reached <paramref name="timeout"/> still throws
    /// <see cref="TimeoutException"/>; this token is not linked to the validation token, so a
    /// queued task is not reported as mid-run cancellation.
    /// </param>
    /// <exception cref="TimeoutException">
    /// Thrown when no attempt observes mid-run cancellation within <paramref name="timeout"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled before the timeout.
    /// </exception>
    public static async Task AssertCanceledMidRunAsync(
        Func<CancellationToken, Task> validateAsync,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var deadline = timeout ?? TimeSpan.FromSeconds(30);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var cts = new CancellationTokenSource();
            var task = validateAsync(cts.Token);

            // Wait until the delegate has actually started (or already finished) so that
            // cancellation, when it lands, is observed at an in-loop checkpoint rather than
            // at task scheduling.
            while ((task.Status == TaskStatus.WaitingToRun ||
                    task.Status == TaskStatus.WaitingForActivation) &&
                   stopwatch.Elapsed < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
                await WaitForCompletionAsync(task, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return;
            }

            // Validation completed before cancellation was observed; try again with a fresh
            // token source until we win the race or run out of time.
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new TimeoutException(
            "Validation never observed mid-run cancellation within the timeout.");
    }

    /// <summary>
    /// Waits for <paramref name="task"/> and stops waiting when <paramref name="cancellationToken"/>
    /// is cancelled. The token is not linked into <paramref name="task"/> itself.
    /// </summary>
    private static async Task WaitForCompletionAsync(Task task, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            await task;
            return;
        }

        // Delay(-1) stays pending until its token is cancelled. Cancel the linked source
        // when the work finishes so the timer does not outlive this wait.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var cancelWait = Task.Delay(-1, linked.Token);
        var finished = await Task.WhenAny(task, cancelWait);
        linked.Cancel();
        try
        {
            await cancelWait;
        }
        catch (OperationCanceledException)
        {
            // The wait was released. A cancelled caller token is reported below.
        }

        if (!ReferenceEquals(finished, task))
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        await task;
    }
}
