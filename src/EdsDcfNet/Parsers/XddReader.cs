namespace EdsDcfNet.Parsers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;

#pragma warning disable CA1845, CA1865, CA1866 // span-based and char overloads not available in netstandard2.0
#pragma warning disable CA2249 // string.Contains(string, StringComparison) not available in netstandard2.0; IndexOf is the correct alternative
#pragma warning disable CA1846 // AsSpan not available in netstandard2.0

/// <summary>
/// Reader for CiA 311 XDD (XML Device Description) files.
/// </summary>
public class XddReader
{
    /// <summary>
    /// Reads an XDD file from the specified path.
    /// </summary>
    /// <param name="filePath">Path to the XDD file</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
    /// <exception cref="EdsParseException">Thrown when the XDD content is invalid</exception>
    public ElectronicDataSheet ReadFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"XDD file not found: {filePath}", filePath);

        var content = File.ReadAllText(filePath, Encoding.UTF8);
        return ReadString(content);
    }

    /// <summary>
    /// Reads an XDD from a string.
    /// </summary>
    /// <param name="content">XDD file content as string</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    /// <exception cref="EdsParseException">Thrown when the XDD content is invalid</exception>
    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Public API — instance method for consistency with EdsReader pattern.")]
    public ElectronicDataSheet ReadString(string content)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(content);
        }
        catch (Exception ex)
        {
            throw new EdsParseException("Failed to parse XDD XML content.", ex);
        }

        return ParseDocument(doc, includeActualValues: false);
    }

    /// <summary>
    /// Parses an XDocument into an ElectronicDataSheet.
    /// </summary>
    /// <param name="doc">The XDocument to parse</param>
    /// <param name="includeActualValues">If true, actualValue attributes are mapped to ParameterValue</param>
    /// <returns>Parsed ElectronicDataSheet object</returns>
    internal static ElectronicDataSheet ParseDocument(XDocument doc, bool includeActualValues)
    {
        var root = doc.Root;
        if (root == null)
            throw new EdsParseException("XDD document has no root element.");

        var profiles = root.Elements()
            .Where(e => e.Name.LocalName == "ISO15745Profile")
            .ToList();

        XElement? deviceProfileBody = null;
        XElement? commNetProfileBody = null;

        foreach (var profile in profiles)
        {
            var profileBody = profile.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "ProfileBody");
            if (profileBody == null)
                continue;

            var xsiType = GetXsiType(profileBody);
            if (xsiType.IndexOf("ProfileBody_Device_CANopen", StringComparison.OrdinalIgnoreCase) >= 0)
                deviceProfileBody = profileBody;
            else if (xsiType.IndexOf("ProfileBody_CommunicationNetwork_CANopen", StringComparison.OrdinalIgnoreCase) >= 0)
                commNetProfileBody = profileBody;
        }

        if (commNetProfileBody == null)
            throw new EdsParseException("XDD document does not contain a CommunicationNetwork ProfileBody.");

        var eds = new ElectronicDataSheet();

        // Parse FileInfo from device profile body (preferred) or comm-net body
        var fileInfoSource = deviceProfileBody ?? commNetProfileBody;
        eds.FileInfo = ParseFileInfo(fileInfoSource);

        // Parse DeviceIdentity from device profile body
        if (deviceProfileBody != null)
        {
            eds.DeviceInfo = ParseDeviceIdentity(deviceProfileBody);
            eds.ApplicationProcessXml = ParseApplicationProcessXml(deviceProfileBody);
        }

        // Parse communication features and object dictionary from comm-net profile body
        ParseCommNetProfile(commNetProfileBody, eds, includeActualValues);

        return eds;
    }

    private static EdsFileInfo ParseFileInfo(XElement profileBody)
    {
        var fileInfo = new EdsFileInfo();

        fileInfo.FileName = profileBody.Attribute("fileName")?.Value ?? string.Empty;
        fileInfo.CreatedBy = profileBody.Attribute("fileCreator")?.Value ?? string.Empty;
        fileInfo.ModifiedBy = profileBody.Attribute("fileModifiedBy")?.Value ?? string.Empty;

        // fileVersion is a string like "1.0" or "1" — map to byte via FileVersion
        var fileVersionStr = profileBody.Attribute("fileVersion")?.Value ?? string.Empty;
        if (!string.IsNullOrEmpty(fileVersionStr))
        {
            // If it looks like "1.0", take the part before the dot
            var dotIdx = fileVersionStr.IndexOf('.');
            var majorPart = dotIdx >= 0 ? fileVersionStr.Substring(0, dotIdx) : fileVersionStr;
            if (byte.TryParse(majorPart, NumberStyles.None, CultureInfo.InvariantCulture, out var ver))
                fileInfo.FileVersion = ver;
        }

        // fileCreationDate is xsd:date "YYYY-MM-DD" → convert to EDS "MM-DD-YYYY"
        var creationDate = profileBody.Attribute("fileCreationDate")?.Value ?? string.Empty;
        fileInfo.CreationDate = ConvertXsdDateToEds(creationDate);

        var creationTime = profileBody.Attribute("fileCreationTime")?.Value ?? string.Empty;
        fileInfo.CreationTime = creationTime;

        var modDate = profileBody.Attribute("fileModificationDate")?.Value ?? string.Empty;
        fileInfo.ModificationDate = ConvertXsdDateToEds(modDate);

        var modTime = profileBody.Attribute("fileModificationTime")?.Value ?? string.Empty;
        fileInfo.ModificationTime = modTime;

        return fileInfo;
    }

    private static DeviceInfo ParseDeviceIdentity(XElement profileBody)
    {
        var deviceInfo = new DeviceInfo();

        var identity = profileBody.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "DeviceIdentity");
        if (identity == null)
            return deviceInfo;

        deviceInfo.VendorName = GetChildText(identity, "vendorName");
        deviceInfo.ProductName = GetChildText(identity, "productName");

        var vendorIdStr = GetChildText(identity, "vendorID");
        if (!string.IsNullOrEmpty(vendorIdStr))
            deviceInfo.VendorNumber = ParseHexId(vendorIdStr);

        var productIdStr = GetChildText(identity, "productID");
        if (!string.IsNullOrEmpty(productIdStr))
            deviceInfo.ProductNumber = ParseHexId(productIdStr);

        return deviceInfo;
    }

    private static string? ParseApplicationProcessXml(XElement profileBody)
    {
        var appProcess = profileBody.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "ApplicationProcess");
        if (appProcess == null)
            return null;

        return appProcess.ToString(SaveOptions.DisableFormatting);
    }

    private static void ParseCommNetProfile(XElement profileBody, ElectronicDataSheet eds, bool includeActualValues)
    {
        var appLayers = profileBody.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "ApplicationLayers");

        if (appLayers != null)
        {
            // Object dictionary
            var objList = appLayers.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "CANopenObjectList");
            if (objList != null)
                ParseObjectDictionary(objList, eds.ObjectDictionary, includeActualValues);

            // Dummy usage
            var dummyUsage = appLayers.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "dummyUsage");
            if (dummyUsage != null)
                ParseDummyUsage(dummyUsage, eds.ObjectDictionary);

            // Dynamic channels
            var dynChannels = appLayers.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "dynamicChannels");
            if (dynChannels != null)
            {
                var dc = ParseDynamicChannels(dynChannels);
                if (dc != null && dc.Segments.Count > 0)
                    eds.DynamicChannels = dc;
            }
        }

        var transportLayers = profileBody.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "TransportLayers");
        if (transportLayers != null)
            ParseBaudRates(transportLayers, eds.DeviceInfo);

        var networkMgmt = profileBody.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "NetworkManagement");
        if (networkMgmt != null)
            ParseNetworkManagement(networkMgmt, eds.DeviceInfo);
    }

    private static void ParseObjectDictionary(XElement objList, ObjectDictionary dict, bool includeActualValues)
    {
        foreach (var objElem in objList.Elements().Where(e => e.Name.LocalName == "CANopenObject"))
        {
            var obj = ParseCanOpenObject(objElem, includeActualValues);
            dict.Objects[obj.Index] = obj;

            // Classify object into the right list based on index range
            ClassifyObject(dict, obj.Index);
        }
    }

    private static CanOpenObject ParseCanOpenObject(XElement elem, bool includeActualValues)
    {
        var obj = new CanOpenObject();

        var indexStr = elem.Attribute("index")?.Value ?? "0000";
        obj.Index = ParseHexIndex(indexStr);
        obj.ParameterName = elem.Attribute("name")?.Value ?? string.Empty;

        var objTypeStr = elem.Attribute("objectType")?.Value;
        if (!string.IsNullOrEmpty(objTypeStr) &&
            byte.TryParse(objTypeStr, NumberStyles.None, CultureInfo.InvariantCulture, out var objType))
            obj.ObjectType = objType;
        else
            obj.ObjectType = 0x7;

        if (elem.Attribute("dataType")?.Value is string dataTypeStr)
            obj.DataType = ParseHexDataType(dataTypeStr);

        if (elem.Attribute("accessType")?.Value is string accessStr)
            obj.AccessType = ParseXddAccessType(accessStr);

        obj.DefaultValue = elem.Attribute("defaultValue")?.Value;
        obj.LowLimit = elem.Attribute("lowLimit")?.Value;
        obj.HighLimit = elem.Attribute("highLimit")?.Value;

        var pdoMappingStr = elem.Attribute("PDOmapping")?.Value;
        obj.PdoMapping = ParseXddPdoMapping(pdoMappingStr);

        var objFlagsStr = elem.Attribute("objFlags")?.Value;
        if (!string.IsNullOrEmpty(objFlagsStr) &&
            uint.TryParse(objFlagsStr, NumberStyles.None, CultureInfo.InvariantCulture, out var flags))
            obj.ObjFlags = flags;

        var subNumberStr = elem.Attribute("subNumber")?.Value;
        if (!string.IsNullOrEmpty(subNumberStr) &&
            byte.TryParse(subNumberStr, NumberStyles.None, CultureInfo.InvariantCulture, out var subNum))
            obj.SubNumber = subNum;

        if (includeActualValues)
        {
            if (elem.Attribute("actualValue")?.Value is string actualValue)
                obj.ParameterValue = actualValue;

            if (elem.Attribute("denotation")?.Value is string denotation)
                obj.Denotation = denotation;
        }

        // Parse sub-objects
        foreach (var subElem in elem.Elements().Where(e => e.Name.LocalName == "CANopenSubObject"))
        {
            var subObj = ParseCanOpenSubObject(subElem, includeActualValues);
            obj.SubObjects[subObj.SubIndex] = subObj;
        }

        return obj;
    }

    private static CanOpenSubObject ParseCanOpenSubObject(XElement elem, bool includeActualValues)
    {
        var subObj = new CanOpenSubObject();

        var subIndexStr = elem.Attribute("subIndex")?.Value ?? "00";
        subObj.SubIndex = ParseHexSubIndex(subIndexStr);
        subObj.ParameterName = elem.Attribute("name")?.Value ?? string.Empty;

        var objTypeStr = elem.Attribute("objectType")?.Value;
        if (!string.IsNullOrEmpty(objTypeStr) &&
            byte.TryParse(objTypeStr, NumberStyles.None, CultureInfo.InvariantCulture, out var objType))
            subObj.ObjectType = objType;
        else
            subObj.ObjectType = 0x7;

        if (elem.Attribute("dataType")?.Value is string dataTypeStr)
            subObj.DataType = ParseHexDataType(dataTypeStr);

        if (elem.Attribute("accessType")?.Value is string accessStr)
            subObj.AccessType = ParseXddAccessType(accessStr);

        subObj.DefaultValue = elem.Attribute("defaultValue")?.Value;
        subObj.LowLimit = elem.Attribute("lowLimit")?.Value;
        subObj.HighLimit = elem.Attribute("highLimit")?.Value;

        var pdoMappingStr = elem.Attribute("PDOmapping")?.Value;
        subObj.PdoMapping = ParseXddPdoMapping(pdoMappingStr);

        if (includeActualValues)
        {
            if (elem.Attribute("actualValue")?.Value is string actualValue)
                subObj.ParameterValue = actualValue;

            if (elem.Attribute("denotation")?.Value is string denotation)
                subObj.Denotation = denotation;
        }

        return subObj;
    }

    private static void ClassifyObject(ObjectDictionary dict, ushort index)
    {
        // Mandatory objects: 1000h and 1001h
        if (index == 0x1000 || index == 0x1001)
        {
            if (!dict.MandatoryObjects.Contains(index))
                dict.MandatoryObjects.Add(index);
        }
        // Manufacturer-specific objects: 2000h-5FFFh
        else if (index >= 0x2000 && index <= 0x5FFF)
        {
            if (!dict.ManufacturerObjects.Contains(index))
                dict.ManufacturerObjects.Add(index);
        }
        // Everything else goes to optional
        else
        {
            if (!dict.OptionalObjects.Contains(index))
                dict.OptionalObjects.Add(index);
        }
    }

    private static void ParseDummyUsage(XElement dummyUsage, ObjectDictionary dict)
    {
        foreach (var dummy in dummyUsage.Elements().Where(e => e.Name.LocalName == "dummy"))
        {
            var entry = dummy.Attribute("entry")?.Value ?? string.Empty;
            // Format: "DummyXXXX=0" or "DummyXXXX=1"
            var eqIdx = entry.IndexOf('=');
            if (eqIdx < 0)
                continue;

            var keyPart = entry.Substring(0, eqIdx).Trim();
            var valPart = entry.Substring(eqIdx + 1).Trim();

            // keyPart must start with "Dummy" followed by 4 hex digits
            if (!keyPart.StartsWith("Dummy", StringComparison.OrdinalIgnoreCase) || keyPart.Length < 9)
                continue;

            var hexPart = keyPart.Substring(5);
            if (!ushort.TryParse(hexPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var dummyIndex))
                continue;

            dict.DummyUsage[dummyIndex] = valPart == "1";
        }
    }

    private static DynamicChannels? ParseDynamicChannels(XElement dynChannels)
    {
        var result = new DynamicChannels();

        foreach (var chanElem in dynChannels.Elements().Where(e => e.Name.LocalName == "dynamicChannel"))
        {
            var seg = new DynamicChannelSegment();

            if (chanElem.Attribute("dataType")?.Value is string typeStr)
                seg.Type = ParseHexDataType(typeStr);

            if (chanElem.Attribute("accessType")?.Value is string dirStr)
                seg.Dir = ParseXddAccessType(dirStr);

            seg.Range = chanElem.Attribute("startIndex")?.Value ?? string.Empty;
            var endIdx = chanElem.Attribute("endIndex")?.Value;
            if (!string.IsNullOrEmpty(endIdx) && !string.IsNullOrEmpty(seg.Range))
                seg.Range = seg.Range + "-" + endIdx;

            var ppOffsetStr = chanElem.Attribute("pDOmappingIndex")?.Value;
            if (!string.IsNullOrEmpty(ppOffsetStr) &&
                uint.TryParse(ppOffsetStr, NumberStyles.None, CultureInfo.InvariantCulture, out var ppOffset))
                seg.PPOffset = ppOffset;

            result.Segments.Add(seg);
        }

        return result.Segments.Count > 0 ? result : null;
    }

    private static void ParseBaudRates(XElement transportLayers, DeviceInfo deviceInfo)
    {
        var physLayer = transportLayers.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "PhysicalLayer");
        if (physLayer == null)
            return;

        var baudRate = physLayer.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "baudRate");
        if (baudRate == null)
            return;

        foreach (var supported in baudRate.Elements()
            .Where(e => e.Name.LocalName == "supportedBaudRate"))
        {
            var val = supported.Attribute("value")?.Value ?? string.Empty;
            var kbps = ParseBaudRateString(val);
            SetBaudRate(deviceInfo.SupportedBaudRates, kbps);
        }
    }

    private static void ParseNetworkManagement(XElement networkMgmt, DeviceInfo deviceInfo)
    {
        var generalFeatures = networkMgmt.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "CANopenGeneralFeatures");
        if (generalFeatures != null)
        {
            var granStr = generalFeatures.Attribute("granularity")?.Value;
            if (!string.IsNullOrEmpty(granStr) &&
                byte.TryParse(granStr, NumberStyles.None, CultureInfo.InvariantCulture, out var gran))
                deviceInfo.Granularity = gran;

            var rxPdoStr = generalFeatures.Attribute("nrOfRxPDO")?.Value;
            if (!string.IsNullOrEmpty(rxPdoStr) &&
                ushort.TryParse(rxPdoStr, NumberStyles.None, CultureInfo.InvariantCulture, out var rxPdo))
                deviceInfo.NrOfRxPdo = rxPdo;

            var txPdoStr = generalFeatures.Attribute("nrOfTxPDO")?.Value;
            if (!string.IsNullOrEmpty(txPdoStr) &&
                ushort.TryParse(txPdoStr, NumberStyles.None, CultureInfo.InvariantCulture, out var txPdo))
                deviceInfo.NrOfTxPdo = txPdo;

            if (generalFeatures.Attribute("bootUpSlave")?.Value is string bootUpSlaveStr)
                deviceInfo.SimpleBootUpSlave = ParseXmlBool(bootUpSlaveStr);

            if (generalFeatures.Attribute("groupMessaging")?.Value is string groupMsgStr)
                deviceInfo.GroupMessaging = ParseXmlBool(groupMsgStr);

            if (generalFeatures.Attribute("layerSettingServiceSlave")?.Value is string lssStr)
                deviceInfo.LssSupported = ParseXmlBool(lssStr);

            var dynChanStr = generalFeatures.Attribute("dynamicChannels")?.Value;
            if (!string.IsNullOrEmpty(dynChanStr) &&
                byte.TryParse(dynChanStr, NumberStyles.None, CultureInfo.InvariantCulture, out var dynChan))
                deviceInfo.DynamicChannelsSupported = dynChan;
        }

        var masterFeatures = networkMgmt.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "CANopenMasterFeatures");
        if (masterFeatures != null)
        {
            if (masterFeatures.Attribute("bootUpMaster")?.Value is string bootUpMasterStr)
                deviceInfo.SimpleBootUpMaster = ParseXmlBool(bootUpMasterStr);
        }
    }

    internal static DeviceCommissioning? ParseDeviceCommissioning(XElement networkMgmt)
    {
        var dcElem = networkMgmt.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "deviceCommissioning");
        if (dcElem == null)
            return null;

        var dc = new DeviceCommissioning();

        var nodeIdStr = dcElem.Attribute("nodeID")?.Value ?? string.Empty;
        if (!string.IsNullOrEmpty(nodeIdStr))
        {
            byte nodeIdValue;
            bool parsed;
            if (nodeIdStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                parsed = byte.TryParse(nodeIdStr.Substring(2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out nodeIdValue);
            else
                parsed = byte.TryParse(nodeIdStr, NumberStyles.None,
                    CultureInfo.InvariantCulture, out nodeIdValue);

            if (parsed)
            {
                if (nodeIdValue < 1 || nodeIdValue > 127)
                    throw new EdsParseException(
                        $"Invalid nodeID '{nodeIdValue}'. CANopen Node-ID must be in range 1..127.");
                dc.NodeId = nodeIdValue;
            }
        }

        dc.NodeName = dcElem.Attribute("nodeName")?.Value ?? string.Empty;

        var baudrateStr = dcElem.Attribute("actualBaudRate")?.Value ?? string.Empty;
        dc.Baudrate = ParseBaudRateString(baudrateStr);

        var netNumberStr = dcElem.Attribute("networkNumber")?.Value ?? string.Empty;
        if (!string.IsNullOrEmpty(netNumberStr) &&
            uint.TryParse(netNumberStr, NumberStyles.None, CultureInfo.InvariantCulture, out var netNum))
            dc.NetNumber = netNum;

        dc.NetworkName = dcElem.Attribute("networkName")?.Value ?? string.Empty;

        if (dcElem.Attribute("CANopenManager")?.Value is string managerStr)
            dc.CANopenManager = ParseXmlBool(managerStr);

        return dc;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string GetXsiType(XElement element)
    {
        foreach (var attr in element.Attributes())
        {
            if (attr.Name.LocalName == "type")
                return attr.Value;
        }

        return string.Empty;
    }

    private static string GetChildText(XElement parent, string localName)
    {
        var child = parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
        return child?.Value?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Parses a hex index string (e.g. "1000" or "0x1000") to ushort.
    /// </summary>
    internal static ushort ParseHexIndex(string value)
    {
        value = value.Trim();

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(2);

        if (ushort.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
            return result;

        return 0;
    }

    private static byte ParseHexSubIndex(string value)
    {
        value = value.Trim();

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(2);

        if (byte.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
            return result;

        return 0;
    }

    private static ushort ParseHexDataType(string value)
    {
        value = value.Trim();

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(2);

        if (ushort.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
            return result;

        return 0;
    }

    private static uint ParseHexId(string value)
    {
        value = value.Trim();

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(2);

        if (uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
            return result;

        return 0;
    }

    private static AccessType ParseXddAccessType(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "const" => AccessType.Constant,
            "ro" => AccessType.ReadOnly,
            "wo" => AccessType.WriteOnly,
            "rw" => AccessType.ReadWrite,
            _ => AccessType.ReadOnly
        };
    }

    private static bool ParseXddPdoMapping(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return !value!.Equals("no", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ParseXmlBool(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.Ordinal);
    }

    internal static ushort ParseBaudRateString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        value = value.Trim();

        if (value.Equals("10 Kbps", StringComparison.OrdinalIgnoreCase)) return 10;
        if (value.Equals("20 Kbps", StringComparison.OrdinalIgnoreCase)) return 20;
        if (value.Equals("50 Kbps", StringComparison.OrdinalIgnoreCase)) return 50;
        if (value.Equals("125 Kbps", StringComparison.OrdinalIgnoreCase)) return 125;
        if (value.Equals("250 Kbps", StringComparison.OrdinalIgnoreCase)) return 250;
        if (value.Equals("500 Kbps", StringComparison.OrdinalIgnoreCase)) return 500;
        if (value.Equals("800 Kbps", StringComparison.OrdinalIgnoreCase)) return 800;
        if (value.Equals("1000 Kbps", StringComparison.OrdinalIgnoreCase)) return 1000;

        return 0;
    }

    private static void SetBaudRate(BaudRates baudRates, ushort kbps)
    {
        switch (kbps)
        {
            case 10: baudRates.BaudRate10 = true; break;
            case 20: baudRates.BaudRate20 = true; break;
            case 50: baudRates.BaudRate50 = true; break;
            case 125: baudRates.BaudRate125 = true; break;
            case 250: baudRates.BaudRate250 = true; break;
            case 500: baudRates.BaudRate500 = true; break;
            case 800: baudRates.BaudRate800 = true; break;
            case 1000: baudRates.BaudRate1000 = true; break;
        }
    }

    private static string ConvertXsdDateToEds(string xsdDate)
    {
        if (string.IsNullOrEmpty(xsdDate))
            return string.Empty;

        // XSD date: "YYYY-MM-DD" → EDS: "MM-DD-YYYY"
        if (xsdDate.Length >= 10 &&
            xsdDate[4] == '-' && xsdDate[7] == '-')
        {
            var year = xsdDate.Substring(0, 4);
            var month = xsdDate.Substring(5, 2);
            var day = xsdDate.Substring(8, 2);
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2}", month, day, year);
        }

        return xsdDate;
    }
}

#pragma warning restore CA1845, CA1865, CA1866
#pragma warning restore CA2249
#pragma warning restore CA1846
