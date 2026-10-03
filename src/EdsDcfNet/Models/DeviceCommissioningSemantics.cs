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

    /// <summary>
    /// <see langword="true"/> when the DCF writer emits <c>[DeviceComissioning]</c>: the
    /// commissioning data is set, or the section keeps entries of its own
    /// (<see cref="DeviceCommissioning.RemainingEntries"/>) that would otherwise be lost.
    /// </summary>
    /// <remarks>
    /// Only kept entries the writer outputs count. A kept key such as <c>NodeID</c> is written
    /// from its property and suppressed (<see cref="SectionEntryKeys.IsDeviceCommissioningKey"/>,
    /// the same predicate the writer and the write rules use), so it does not require the section.
    /// </remarks>
    public static bool IsWrittenToDcf(DeviceCommissioning commissioning)
        => !IsOmitted(commissioning)
           || commissioning.RemainingEntries.Keys.Any(key => !SectionEntryKeys.IsDeviceCommissioningKey(key));
}
