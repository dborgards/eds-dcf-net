namespace EdsDcfNet.Writers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using EdsDcfNet.Models;

/// <summary>
/// Writer for CiA 311 XDD (XML Device Description) files.
/// </summary>
public class XddWriter
{
    /// <summary>
    /// Writes an ElectronicDataSheet as an XDD file to the specified path.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the XDD file should be written</param>
    public void WriteFile(ElectronicDataSheet eds, string filePath)
    {
        var content = GenerateString(eds);
        File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>
    /// Generates XDD content as a string.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to convert</param>
    /// <returns>XDD content as string</returns>
    public string GenerateString(ElectronicDataSheet eds)
        => GenerateString(eds, commissioning: null);

    /// <summary>
    /// Generates XDD/XDC content as a string, optionally including device commissioning data.
    /// Called by <see cref="XdcWriter"/> to pass commissioning through the virtual call chain
    /// without resorting to mutable instance state.
    /// </summary>
    internal string GenerateString(ElectronicDataSheet eds, DeviceCommissioning? commissioning)
    {
        var doc = BuildDocument(eds, commissioning);
        return SerializeDocument(doc);
    }

    /// <summary>
    /// Convenience overload — delegates to <see cref="BuildDocument(ElectronicDataSheet, DeviceCommissioning?)"/>.
    /// Override <see cref="BuildDocument(ElectronicDataSheet, DeviceCommissioning?)"/> to customise output.
    /// </summary>
    protected XDocument BuildDocument(ElectronicDataSheet eds)
        => BuildDocument(eds, commissioning: null);

    /// <summary>
    /// Builds the XDocument for the given EDS. Override in subclasses to customise output.
    /// </summary>
    protected virtual XDocument BuildDocument(ElectronicDataSheet eds, DeviceCommissioning? commissioning)
    {
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

        var container = new XElement("ISO15745ProfileContainer",
            new XAttribute(XNamespace.Xmlns + "xsi", xsi));

        container.Add(BuildDeviceProfile(eds, xsi));
        container.Add(BuildCommNetProfile(eds, xsi, commissioning));

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            container);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Calls virtual members via instance dispatch.")]
    private XElement BuildDeviceProfile(ElectronicDataSheet eds, XNamespace xsi)
    {
        var profileBody = new XElement("ProfileBody",
            new XAttribute(xsi + "type", "ProfileBody_Device_CANopen"));

        AddFileInfoAttributes(profileBody, eds.FileInfo);

        // DeviceIdentity
        profileBody.Add(BuildDeviceIdentity(eds.DeviceInfo));

        // DeviceManager (empty)
        profileBody.Add(new XElement("DeviceManager"));

        // DeviceFunction (empty)
        profileBody.Add(new XElement("DeviceFunction"));

        // ApplicationProcess (opaque if present)
        if (!string.IsNullOrEmpty(eds.ApplicationProcessXml))
        {
            try
            {
                var appProcessElem = XElement.Parse(eds.ApplicationProcessXml);
                profileBody.Add(appProcessElem);
            }
            catch (XmlException)
            {
                // If it can't be parsed as valid XML, skip it
            }
        }

        return BuildProfile("Device", profileBody);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Calls virtual members via instance dispatch.")]
    private XElement BuildCommNetProfile(ElectronicDataSheet eds, XNamespace xsi, DeviceCommissioning? commissioning)
    {
        var profileBody = new XElement("ProfileBody",
            new XAttribute(xsi + "type", "ProfileBody_CommunicationNetwork_CANopen"));

        AddFileInfoAttributes(profileBody, eds.FileInfo);

        // ApplicationLayers
        profileBody.Add(BuildApplicationLayers(eds));

        // TransportLayers
        profileBody.Add(BuildTransportLayers(eds.DeviceInfo));

        // NetworkManagement
        profileBody.Add(BuildNetworkManagement(eds, commissioning));

        return BuildProfile("CommunicationNetwork", profileBody);
    }

    private static XElement BuildProfile(string classId, XElement profileBody)
    {
        return new XElement("ISO15745Profile",
            new XElement("ProfileHeader",
                new XElement("ProfileIdentification", string.Empty),
                new XElement("ProfileRevision", "1"),
                new XElement("ProfileName", string.Empty),
                new XElement("ProfileSource", string.Empty),
                new XElement("ProfileClassID", classId),
                new XElement("ISO15745Reference",
                    new XElement("ISO15745Part", "1"),
                    new XElement("ISO15745Edition", "1"),
                    new XElement("ProfileTechnology", "CANopen"))),
            profileBody);
    }

    private static void AddFileInfoAttributes(XElement profileBody, EdsFileInfo fileInfo)
    {
        if (!string.IsNullOrEmpty(fileInfo.FileName))
            profileBody.Add(new XAttribute("fileName", fileInfo.FileName));

        if (!string.IsNullOrEmpty(fileInfo.CreatedBy))
            profileBody.Add(new XAttribute("fileCreator", fileInfo.CreatedBy));

        // Convert EDS date "MM-DD-YYYY" to XSD date "YYYY-MM-DD"
        var xsdCreationDate = ConvertEdsDateToXsd(fileInfo.CreationDate);
        if (!string.IsNullOrEmpty(xsdCreationDate))
            profileBody.Add(new XAttribute("fileCreationDate", xsdCreationDate));

        if (!string.IsNullOrEmpty(fileInfo.CreationTime))
            profileBody.Add(new XAttribute("fileCreationTime", fileInfo.CreationTime));

        profileBody.Add(new XAttribute("fileVersion",
            fileInfo.FileVersion.ToString(CultureInfo.InvariantCulture)));

        var xsdModDate = ConvertEdsDateToXsd(fileInfo.ModificationDate);
        if (!string.IsNullOrEmpty(xsdModDate))
            profileBody.Add(new XAttribute("fileModificationDate", xsdModDate));

        if (!string.IsNullOrEmpty(fileInfo.ModificationTime))
            profileBody.Add(new XAttribute("fileModificationTime", fileInfo.ModificationTime));

        if (!string.IsNullOrEmpty(fileInfo.ModifiedBy))
            profileBody.Add(new XAttribute("fileModifiedBy", fileInfo.ModifiedBy));
    }

    private static XElement BuildDeviceIdentity(DeviceInfo deviceInfo)
    {
        return new XElement("DeviceIdentity",
            new XElement("vendorName", deviceInfo.VendorName),
            new XElement("vendorID",
                string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", deviceInfo.VendorNumber)),
            new XElement("productName", deviceInfo.ProductName),
            new XElement("productID",
                string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", deviceInfo.ProductNumber)));
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Calls virtual members via instance dispatch.")]
    private XElement BuildApplicationLayers(ElectronicDataSheet eds)
    {
        var appLayers = new XElement("ApplicationLayers");

        // CANopenObjectList
        appLayers.Add(BuildObjectList(eds.ObjectDictionary));

        // dummyUsage
        if (eds.ObjectDictionary.DummyUsage.Count > 0)
            appLayers.Add(BuildDummyUsage(eds.ObjectDictionary));

        // dynamicChannels
        if (eds.DynamicChannels != null && eds.DynamicChannels.Segments.Count > 0)
            appLayers.Add(BuildDynamicChannels(eds.DynamicChannels));

        return appLayers;
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Calls virtual members via instance dispatch.")]
    private XElement BuildObjectList(ObjectDictionary dict)
    {
        var objList = new XElement("CANopenObjectList",
            new XAttribute("mandatoryObjects",
                dict.MandatoryObjects.Count.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("optionalObjects",
                dict.OptionalObjects.Count.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("manufacturerObjects",
                dict.ManufacturerObjects.Count.ToString(CultureInfo.InvariantCulture)));

        foreach (var obj in dict.Objects.OrderBy(o => o.Key).Select(o => o.Value))
        {
            objList.Add(BuildCanOpenObject(obj));
        }

        return objList;
    }

    /// <summary>
    /// Builds a CANopenObject XElement. Override in subclasses to add extra attributes (e.g. actualValue).
    /// </summary>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "Parameter name is a CANopen domain term, not a VB keyword conflict in context.")]
    protected virtual XElement BuildCanOpenObject(CanOpenObject obj)
    {
        var elem = new XElement("CANopenObject");

        elem.Add(new XAttribute("index", FormatIndex(obj.Index)));
        elem.Add(new XAttribute("name", obj.ParameterName));
        elem.Add(new XAttribute("objectType",
            obj.ObjectType.ToString(CultureInfo.InvariantCulture)));

        if (obj.DataType.HasValue)
            elem.Add(new XAttribute("dataType", FormatDataType(obj.DataType.Value)));

        // Only write accessType for objects with a data type (VAR-like)
        if (obj.DataType.HasValue)
            elem.Add(new XAttribute("accessType", XddAccessTypeToString(obj.AccessType)));

        if (!string.IsNullOrEmpty(obj.DefaultValue))
            elem.Add(new XAttribute("defaultValue", obj.DefaultValue));

        if (!string.IsNullOrEmpty(obj.LowLimit))
            elem.Add(new XAttribute("lowLimit", obj.LowLimit));

        if (!string.IsNullOrEmpty(obj.HighLimit))
            elem.Add(new XAttribute("highLimit", obj.HighLimit));

        if (obj.DataType.HasValue)
            elem.Add(new XAttribute("PDOmapping", obj.PdoMapping ? "optional" : "no"));

        if (obj.ObjFlags > 0)
            elem.Add(new XAttribute("objFlags",
                obj.ObjFlags.ToString(CultureInfo.InvariantCulture)));

        if (obj.SubNumber.HasValue)
            elem.Add(new XAttribute("subNumber",
                obj.SubNumber.Value.ToString(CultureInfo.InvariantCulture)));

        AddCanOpenObjectXdcAttributes(elem, obj);

        // Sub-objects
        foreach (var subObj in obj.SubObjects.OrderBy(s => s.Key).Select(s => s.Value))
        {
            elem.Add(BuildCanOpenSubObject(subObj));
        }

        return elem;
    }

    /// <summary>
    /// Hook for subclasses to add extra attributes (e.g. actualValue/denotation) to CANopenObject elements.
    /// </summary>
    protected virtual void AddCanOpenObjectXdcAttributes(XElement elem, CanOpenObject obj)
    {
        // Base implementation does nothing
    }

    /// <summary>
    /// Builds a CANopenSubObject XElement. Override in subclasses to add extra attributes.
    /// </summary>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "Parameter name is a CANopen domain term; VB conflict not applicable here.")]
    protected virtual XElement BuildCanOpenSubObject(CanOpenSubObject subObject)
    {
        var elem = new XElement("CANopenSubObject");

        elem.Add(new XAttribute("subIndex",
            subObject.SubIndex.ToString("X2", CultureInfo.InvariantCulture)));
        elem.Add(new XAttribute("name", subObject.ParameterName));
        elem.Add(new XAttribute("objectType",
            subObject.ObjectType.ToString(CultureInfo.InvariantCulture)));
        elem.Add(new XAttribute("dataType", FormatDataType(subObject.DataType)));
        elem.Add(new XAttribute("accessType", XddAccessTypeToString(subObject.AccessType)));

        if (!string.IsNullOrEmpty(subObject.DefaultValue))
            elem.Add(new XAttribute("defaultValue", subObject.DefaultValue));

        if (!string.IsNullOrEmpty(subObject.LowLimit))
            elem.Add(new XAttribute("lowLimit", subObject.LowLimit));

        if (!string.IsNullOrEmpty(subObject.HighLimit))
            elem.Add(new XAttribute("highLimit", subObject.HighLimit));

        elem.Add(new XAttribute("PDOmapping", subObject.PdoMapping ? "optional" : "no"));

        AddCanOpenSubObjectXdcAttributes(elem, subObject);

        return elem;
    }

    /// <summary>
    /// Hook for subclasses to add extra attributes to CANopenSubObject elements.
    /// </summary>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "Parameter name is a CANopen domain term; VB conflict not applicable here.")]
    protected virtual void AddCanOpenSubObjectXdcAttributes(XElement elem, CanOpenSubObject subObject)
    {
        // Base implementation does nothing
    }

    private static XElement BuildDummyUsage(ObjectDictionary dict)
    {
        var dummyElem = new XElement("dummyUsage");

        foreach (var kvp in dict.DummyUsage.OrderBy(d => d.Key))
        {
            dummyElem.Add(new XElement("dummy",
                new XAttribute("entry",
                    string.Format(CultureInfo.InvariantCulture,
                        "Dummy{0:X4}={1}", kvp.Key, kvp.Value ? "1" : "0"))));
        }

        return dummyElem;
    }

    private static XElement BuildDynamicChannels(DynamicChannels channels)
    {
        var dynElem = new XElement("dynamicChannels");

        foreach (var seg in channels.Segments)
        {
            var chanElem = new XElement("dynamicChannel",
                new XAttribute("dataType", FormatDataType(seg.Type)),
                new XAttribute("accessType", XddAccessTypeToString(seg.Dir)));

            // Parse Range back to startIndex/endIndex
            var rangeParts = seg.Range.Split('-');
            if (rangeParts.Length >= 1)
                chanElem.Add(new XAttribute("startIndex", rangeParts[0].Trim()));
            if (rangeParts.Length >= 2)
                chanElem.Add(new XAttribute("endIndex", rangeParts[1].Trim()));

            if (seg.PPOffset > 0)
                chanElem.Add(new XAttribute("pDOmappingIndex",
                    seg.PPOffset.ToString(CultureInfo.InvariantCulture)));

            dynElem.Add(chanElem);
        }

        return dynElem;
    }

    private static XElement BuildTransportLayers(DeviceInfo deviceInfo)
    {
        var defaultBaudRate = GetDefaultBaudRateString(deviceInfo.SupportedBaudRates);

        var baudRateElem = new XElement("baudRate",
            new XAttribute("defaultValue", defaultBaudRate));

        var hasSupported = false;
        foreach (var kbps in GetSupportedBaudRates(deviceInfo.SupportedBaudRates))
        {
            hasSupported = true;
            baudRateElem.Add(new XElement("supportedBaudRate",
                new XAttribute("value", FormatBaudRate(kbps))));
        }

        if (!hasSupported)
        {
            // No baud-rate flags set: emit the fallback default as a supported entry
            // so the XML is self-consistent (defaultValue must appear in supportedBaudRate).
            baudRateElem.Add(new XElement("supportedBaudRate",
                new XAttribute("value", defaultBaudRate)));
        }

        return new XElement("TransportLayers",
            new XElement("PhysicalLayer",
                baudRateElem));
    }

    /// <summary>
    /// Builds the NetworkManagement element.
    /// Subclasses can override to inspect <paramref name="commissioning"/> and append
    /// a deviceCommissioning child when it is non-null.
    /// </summary>
    protected virtual XElement BuildNetworkManagement(ElectronicDataSheet eds, DeviceCommissioning? commissioning)
    {
        var networkMgmt = new XElement("NetworkManagement");
        networkMgmt.Add(BuildGeneralFeatures(eds.DeviceInfo));
        networkMgmt.Add(BuildMasterFeatures(eds.DeviceInfo));
        return networkMgmt;
    }

    private static XElement BuildGeneralFeatures(DeviceInfo deviceInfo)
    {
        return new XElement("CANopenGeneralFeatures",
            new XAttribute("granularity",
                deviceInfo.Granularity.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("nrOfRxPDO",
                deviceInfo.NrOfRxPdo.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("nrOfTxPDO",
                deviceInfo.NrOfTxPdo.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("bootUpSlave",
                deviceInfo.SimpleBootUpSlave ? "true" : "false"),
            new XAttribute("layerSettingServiceSlave",
                deviceInfo.LssSupported ? "true" : "false"),
            new XAttribute("groupMessaging",
                deviceInfo.GroupMessaging ? "true" : "false"),
            new XAttribute("dynamicChannels",
                deviceInfo.DynamicChannelsSupported.ToString(CultureInfo.InvariantCulture)));
    }

    private static XElement BuildMasterFeatures(DeviceInfo deviceInfo)
    {
        return new XElement("CANopenMasterFeatures",
            new XAttribute("bootUpMaster",
                deviceInfo.SimpleBootUpMaster ? "true" : "false"));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Formats a 16-bit index as 4 uppercase hex digits (e.g. "1000").</summary>
    protected static string FormatIndex(ushort index) =>
        index.ToString("X4", CultureInfo.InvariantCulture);

    /// <summary>Formats a data type as 4 uppercase hex digits (e.g. "0007").</summary>
    protected static string FormatDataType(ushort dataType) =>
        dataType.ToString("X4", CultureInfo.InvariantCulture);

    /// <summary>
    /// Converts an AccessType to XDD access type string.
    /// ReadWriteInput/ReadWriteOutput have no XDD equivalent → mapped to "rw".
    /// </summary>
    protected static string XddAccessTypeToString(AccessType accessType) =>
        accessType switch
        {
            AccessType.Constant => "const",
            AccessType.ReadOnly => "ro",
            AccessType.WriteOnly => "wo",
            AccessType.ReadWrite => "rw",
            AccessType.ReadWriteInput => "rw",
            AccessType.ReadWriteOutput => "rw",
            _ => "rw"
        };

    private static string ConvertEdsDateToXsd(string edsDate)
    {
        if (string.IsNullOrEmpty(edsDate))
            return string.Empty;

        // EDS date: "MM-DD-YYYY" → XSD date: "YYYY-MM-DD"
        if (edsDate.Length >= 10 &&
            edsDate[2] == '-' && edsDate[5] == '-')
        {
            var month = edsDate.Substring(0, 2);
            var day = edsDate.Substring(3, 2);
            var year = edsDate.Substring(6, 4);
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2}", year, month, day);
        }

        return edsDate;
    }

    private static IEnumerable<ushort> GetSupportedBaudRates(BaudRates baudRates)
    {
        if (baudRates.BaudRate10) yield return 10;
        if (baudRates.BaudRate20) yield return 20;
        if (baudRates.BaudRate50) yield return 50;
        if (baudRates.BaudRate125) yield return 125;
        if (baudRates.BaudRate250) yield return 250;
        if (baudRates.BaudRate500) yield return 500;
        if (baudRates.BaudRate800) yield return 800;
        if (baudRates.BaudRate1000) yield return 1000;
    }

    private static string GetDefaultBaudRateString(BaudRates baudRates)
    {
        if (baudRates.BaudRate250) return "250 Kbps";
        if (baudRates.BaudRate500) return "500 Kbps";
        if (baudRates.BaudRate125) return "125 Kbps";
        if (baudRates.BaudRate1000) return "1000 Kbps";
        if (baudRates.BaudRate800) return "800 Kbps";
        if (baudRates.BaudRate50) return "50 Kbps";
        if (baudRates.BaudRate20) return "20 Kbps";
        if (baudRates.BaudRate10) return "10 Kbps";
        return "250 Kbps";
    }

    internal static string FormatBaudRate(ushort kbps) =>
        string.Format(CultureInfo.InvariantCulture, "{0} Kbps", kbps);

    private static string SerializeDocument(XDocument doc)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            OmitXmlDeclaration = false
        };

        using var sb = new StringBuilderWriter();
        using (var writer = XmlWriter.Create(sb, settings))
        {
            doc.Save(writer);
        }

        return sb.ToString();
    }

    /// <summary>Helper to write XML to a StringBuilder.</summary>
    private sealed class StringBuilderWriter : System.IO.TextWriter
    {
        private readonly StringBuilder _sb = new();

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => _sb.Append(value);
        public override void Write(string? value) => _sb.Append(value);
        public override void Write(char[] buffer, int index, int count) =>
            _sb.Append(buffer, index, count);

        public override string ToString() => _sb.ToString();
    }
}
