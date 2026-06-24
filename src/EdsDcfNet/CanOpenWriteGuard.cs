namespace EdsDcfNet;

using EdsDcfNet.Models;

/// <summary>
/// Shared pre-write validation for format-specific operations entry points.
/// </summary>
internal static class CanOpenWriteGuard
{
    internal static void EnsureValidEdsForWrite(ElectronicDataSheet eds, CanOpenWriteOptions? options)
    {
        if (options?.ValidateBeforeWrite == true)
            CanOpenFile.EnsureValid(eds);
    }

    internal static void EnsureValidDcfForWrite(DeviceConfigurationFile dcf, CanOpenWriteOptions? options)
    {
        if (options?.ValidateBeforeWrite == true)
            CanOpenFile.EnsureValid(dcf);
    }

    internal static void EnsureValidCpjForWrite(NodelistProject cpj, CanOpenWriteOptions? options)
    {
        if (options?.ValidateBeforeWrite == true)
            CanOpenFile.EnsureValid(cpj);
    }
}
