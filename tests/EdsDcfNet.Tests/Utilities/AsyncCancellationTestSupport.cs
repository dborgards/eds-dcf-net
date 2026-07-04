namespace EdsDcfNet.Tests.Utilities;

/// <summary>
/// Helpers for deterministic async cancellation tests that must cancel while work is in progress.
/// </summary>
internal static class AsyncCancellationTestSupport
{
    /// <summary>
    /// Starts <paramref name="validateAsync"/> with <paramref name="cts"/>, waits until the
    /// returned task is executing on a thread-pool worker, then cancels and asserts
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <exception cref="TimeoutException">
    /// Thrown when the validation task does not reach <see cref="TaskStatus.Running"/> in time.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when validation completes before mid-run cancellation can be applied.
    /// </exception>
    public static async Task AssertCanceledMidRunAsync(
        CancellationTokenSource cts,
        Func<CancellationToken, Task> validateAsync,
        TimeSpan? startTimeout = null)
    {
        var timeout = startTimeout ?? TimeSpan.FromSeconds(5);
        var task = validateAsync(cts.Token);

        using var startTimeoutCts = new CancellationTokenSource(timeout);
        try
        {
            while (!task.IsCompleted)
            {
                startTimeoutCts.Token.ThrowIfCancellationRequested();

                if (task.Status == TaskStatus.Running)
                    break;

                await Task.Yield();
            }
        }
        catch (OperationCanceledException) when (startTimeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException(
                "Validation task did not reach Running state before timeout.");
        }

        if (task.IsCompletedSuccessfully)
        {
            throw new InvalidOperationException(
                "Validation completed before mid-run cancellation could be applied.");
        }

        cts.Cancel();

        var act = () => task;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
