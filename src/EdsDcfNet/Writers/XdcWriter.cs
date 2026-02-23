namespace EdsDcfNet.Writers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using EdsDcfNet.Models;

/// <summary>
/// Writer for CiA 311 XDC (XML Device Configuration) files.
/// Extends XddWriter with actualValue, denotation, and deviceCommissioning support.
/// </summary>
public class XdcWriter : XddWriter
{
    /// <summary>
    /// Writes a DeviceConfigurationFile as an XDC file to the specified path.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the XDC file should be written</param>
    public void WriteFile(DeviceConfigurationFile dcf, string filePath)
    {
        var content = GenerateString(dcf);
        File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>
    /// Generates XDC content as a string.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to convert</param>
    /// <returns>XDC content as string</returns>
    public string GenerateString(DeviceConfigurationFile dcf)
    {
        return base.GenerateString(CreateEdsView(dcf), dcf.DeviceCommissioning);
    }

    /// <inheritdoc/>
    protected override void AddCanOpenObjectXdcAttributes(XElement elem, CanOpenObject obj)
    {
        if (!string.IsNullOrEmpty(obj.ParameterValue))
            elem.Add(new XAttribute("actualValue", obj.ParameterValue));

        if (!string.IsNullOrEmpty(obj.Denotation))
            elem.Add(new XAttribute("denotation", obj.Denotation));
    }

    /// <inheritdoc/>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "Parameter name is a CANopen domain term; VB conflict not applicable here.")]
    protected override void AddCanOpenSubObjectXdcAttributes(XElement elem, CanOpenSubObject subObject)
    {
        if (!string.IsNullOrEmpty(subObject.ParameterValue))
            elem.Add(new XAttribute("actualValue", subObject.ParameterValue));

        if (!string.IsNullOrEmpty(subObject.Denotation))
            elem.Add(new XAttribute("denotation", subObject.Denotation));
    }

    /// <inheritdoc/>
    protected override XElement BuildNetworkManagement(ElectronicDataSheet eds, DeviceCommissioning? commissioning)
    {
        var networkMgmt = base.BuildNetworkManagement(eds, commissioning);

        if (commissioning != null)
            networkMgmt.Add(BuildDeviceCommissioning(commissioning));

        return networkMgmt;
    }

    private static XElement BuildDeviceCommissioning(DeviceCommissioning dc)
    {
        var elem = new XElement("deviceCommissioning");

        elem.Add(new XAttribute("nodeID",
            dc.NodeId.ToString(CultureInfo.InvariantCulture)));

        if (!string.IsNullOrEmpty(dc.NodeName))
            elem.Add(new XAttribute("nodeName", dc.NodeName));

        if (dc.Baudrate > 0)
            elem.Add(new XAttribute("actualBaudRate",
                string.Format(CultureInfo.InvariantCulture, "{0} Kbps", dc.Baudrate)));

        elem.Add(new XAttribute("networkNumber",
            dc.NetNumber.ToString(CultureInfo.InvariantCulture)));

        if (!string.IsNullOrEmpty(dc.NetworkName))
            elem.Add(new XAttribute("networkName", dc.NetworkName));

        elem.Add(new XAttribute("CANopenManager",
            dc.CANopenManager ? "true" : "false"));

        return elem;
    }

    /// <summary>Creates a temporary ElectronicDataSheet view from a DeviceConfigurationFile.</summary>
    private static ElectronicDataSheet CreateEdsView(DeviceConfigurationFile dcf)
    {
        var eds = new ElectronicDataSheet
        {
            FileInfo = dcf.FileInfo,
            DeviceInfo = dcf.DeviceInfo,
            ObjectDictionary = dcf.ObjectDictionary,
            Comments = dcf.Comments,
            DynamicChannels = dcf.DynamicChannels,
            ApplicationProcessXml = dcf.ApplicationProcessXml
        };

        eds.SupportedModules.AddRange(dcf.SupportedModules);
        eds.Tools.AddRange(dcf.Tools);
        foreach (var kvp in dcf.AdditionalSections)
            eds.AdditionalSections[kvp.Key] = kvp.Value;

        return eds;
    }
}
