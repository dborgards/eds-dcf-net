namespace EdsDcfNet.Models;

internal static class DeviceCommissioningSemantics
{
    public static bool IsOmitted(DeviceCommissioning commissioning)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(commissioning);
#else
        if (commissioning is null)
        {
            throw new ArgumentNullException(nameof(commissioning));
        }
#endif

        return commissioning.NodeId == 0 &&
               string.IsNullOrEmpty(commissioning.NodeName) &&
               commissioning.Baudrate == 0 &&
               commissioning.NetNumber == 0 &&
               string.IsNullOrEmpty(commissioning.NetworkName) &&
               !commissioning.CANopenManager &&
               commissioning.LssSerialNumber is null &&
               string.IsNullOrEmpty(commissioning.NodeRefd) &&
               string.IsNullOrEmpty(commissioning.NetRefd);
    }
}
