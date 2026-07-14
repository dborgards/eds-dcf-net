namespace EdsDcfNet;

using EdsDcfNet.Models;

/// <summary>
/// Shared pre-write validation for format-specific operations entry points.
/// </summary>
internal static class CanOpenWriteGuard
{
    internal static void EnsureValidForWrite<T>(T model, CanOpenWriteOptions? options)
    {
        if (!ShouldValidateBeforeWrite(options))
            return;

        ValidateKnownModel(model);
    }

    internal static Task EnsureValidForWriteAsync<T>(
        T model,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldValidateBeforeWrite(options))
            return Task.CompletedTask;

        return ValidateKnownModelAsync(model, cancellationToken);
    }

    internal static bool ShouldValidateBeforeWrite(CanOpenWriteOptions? options) =>
        options?.ValidateBeforeWrite == true;

    private static void ValidateKnownModel(object? model)
    {
        ThrowIfNull(model, nameof(model));

        switch (model!)
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
                    "Unsupported model type: " + model!.GetType().Name,
                    nameof(model));
        }
    }

    private static Task ValidateKnownModelAsync(object? model, CancellationToken cancellationToken)
    {
        ThrowIfNull(model, nameof(model));

        return model! switch
        {
            ElectronicDataSheet eds => CanOpenFile.EnsureValidAsync(eds, cancellationToken),
            DeviceConfigurationFile dcf => CanOpenFile.EnsureValidAsync(dcf, cancellationToken),
            NodelistProject cpj => CanOpenFile.EnsureValidAsync(cpj, cancellationToken),
            _ => throw new ArgumentException(
                "Unsupported model type: " + model!.GetType().Name,
                nameof(model))
        };
    }

    private static void ThrowIfNull(object? value, string paramName)
    {
        if (value is null)
            throw new ArgumentNullException(paramName);
    }
}
