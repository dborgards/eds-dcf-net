namespace EdsDcfNet;

using EdsDcfNet.Models;

/// <summary>
/// Shared pre-write validation for format-specific operations entry points.
/// </summary>
internal static class CanOpenWriteGuard
{
    internal static void EnsureValidForWrite<T>(T model, CanOpenWriteOptions? options)
    {
        if (options?.ValidateBeforeWrite != true)
            return;

        switch (model)
        {
            case ElectronicDataSheet eds:
                CanOpenFile.EnsureValid(eds);
                break;
            case DeviceConfigurationFile dcf:
                CanOpenFile.EnsureValid(dcf);
                break;
            case NodelistProject cpj:
                CanOpenFile.EnsureValid(cpj);
                break;
            default:
                throw new ArgumentException(
                    "Unsupported model type: " + typeof(T).Name,
                    nameof(model));
        }
    }

    internal static Task EnsureValidForWriteAsync<T>(
        T model,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        if (options?.ValidateBeforeWrite != true)
            return Task.CompletedTask;

        return model switch
        {
            ElectronicDataSheet eds => CanOpenFile.EnsureValidAsync(eds, cancellationToken),
            DeviceConfigurationFile dcf => CanOpenFile.EnsureValidAsync(dcf, cancellationToken),
            NodelistProject cpj => CanOpenFile.EnsureValidAsync(cpj, cancellationToken),
            _ => throw new ArgumentException(
                "Unsupported model type: " + typeof(T).Name,
                nameof(model))
        };
    }
}
