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
}
