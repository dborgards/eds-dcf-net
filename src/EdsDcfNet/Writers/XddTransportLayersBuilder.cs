namespace EdsDcfNet.Writers;

using System.Xml.Linq;
using EdsDcfNet.Models;

/// <summary>
/// Builds the XDD <c>TransportLayers</c> XML subtree from <see cref="DeviceInfo"/>.
/// </summary>
internal static class XddTransportLayersBuilder
{
    /// <summary>Builds the <c>TransportLayers</c> element including baud rate declarations.</summary>
    internal static XElement Build(DeviceInfo deviceInfo)
    {
        var defaultBaudRate = XddFormatHelper.GetDefaultBaudRateString(deviceInfo.SupportedBaudRates);

        var transportName = XddNames.ChildOfType(XddNames.NetworkProfileBodyType, "TransportLayers");
        var physicalName = XddNames.Child(transportName, "PhysicalLayer");
        var baudRateElem = XddNames.Element(physicalName, "baudRate",
            new XAttribute("defaultValue", defaultBaudRate));

        var hasSupported = false;
        foreach (var supported in XddFormatHelper.GetSupportedBaudRates(deviceInfo.SupportedBaudRates))
        {
            hasSupported = true;
            baudRateElem.Add(XddNames.Element(baudRateElem.Name, "supportedBaudRate",
                new XAttribute("value", supported)));
        }

        if (!hasSupported)
        {
            // No baud-rate flags set: emit the fallback default as a supported entry
            // so the XML is self-consistent (defaultValue must appear in supportedBaudRate).
            baudRateElem.Add(XddNames.Element(baudRateElem.Name, "supportedBaudRate",
                new XAttribute("value", defaultBaudRate)));
        }

        return new XElement(transportName,
            new XElement(physicalName,
                baudRateElem));
    }
}
