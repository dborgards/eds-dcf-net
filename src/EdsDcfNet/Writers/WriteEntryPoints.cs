namespace EdsDcfNet.Writers;

/// <summary>
/// Shared error wrapping for the <c>WriteFile</c>/<c>WriteStream</c> (and async) entry points of
/// the five format writers. Each writer keeps its public signatures and argument checks; only
/// the identical try/catch scaffolding lives here. The helper is stateless, so the writers'
/// thread-safety contract is unchanged.
/// </summary>
internal static class WriteEntryPoints
{
    internal static void ToFile<TException>(
        string filePath,
        string format,
        Action write,
        Func<string, Exception, TException> createException)
        where TException : Exception
    {
        try
        {
            write();
        }
        catch (TException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw createException($"Failed to write {format} file to {filePath}", ex);
        }
    }

    internal static void ToStream<TException>(
        string format,
        Action write,
        Func<string, Exception, TException> createException)
        where TException : Exception
    {
        try
        {
            write();
        }
        catch (TException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw createException($"Failed to write {format} content to stream.", ex);
        }
    }

    internal static async Task ToFileAsync<TException>(
        string filePath,
        string format,
        Func<Task> write,
        Func<string, Exception, TException> createException,
        CancellationToken cancellationToken)
        where TException : Exception
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await write().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw createException($"Failed to write {format} file to {filePath}", ex);
        }
    }

    internal static async Task ToStreamAsync<TException>(
        string format,
        Func<Task> write,
        Func<string, Exception, TException> createException,
        CancellationToken cancellationToken)
        where TException : Exception
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await write().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw createException($"Failed to write {format} content to stream.", ex);
        }
    }
}
