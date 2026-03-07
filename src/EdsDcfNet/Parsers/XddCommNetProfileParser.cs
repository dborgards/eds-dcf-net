namespace EdsDcfNet.Parsers;

using System.Globalization;
using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using static EdsDcfNet.Parsers.XddParsingPrimitives;

internal static class XddCommNetProfileParser
{
    private static readonly Dictionary<ushort, Action<BaudRates>> BaudRateSetters =
        new Dictionary<ushort, Action<BaudRates>>
        {
            [10] = br => br.BaudRate10 = true,
            [20] = br => br.BaudRate20 = true,
            [50] = br => br.BaudRate50 = true,
            [125] = br => br.BaudRate125 = true,
            [250] = br => br.BaudRate250 = true,
            [500] = br => br.BaudRate500 = true,
            [800] = br => br.BaudRate800 = true,
            [1000] = br => br.BaudRate1000 = true
        };
    internal static void ParseCommNetProfile(XElement profileBody, ElectronicDataSheet eds, bool includeActualValues)
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

        // Preserve unknown ProfileBody children in AdditionalSections for round-trip and XdcReader coverage
        var knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ApplicationLayers",
            "TransportLayers",
            "NetworkManagement"
        };
        foreach (var child in profileBody.Elements())
        {
            if (knownNames.Contains(child.Name.LocalName))
                continue;
            var section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var attr in child.Attributes())
                section[attr.Name.LocalName] = attr.Value;
            eds.AdditionalSections[child.Name.LocalName] = section;
        }
    }

    private static void ParseObjectDictionary(XElement objList, ObjectDictionary dict, bool includeActualValues)
    {
        // HashSets provide O(1) duplicate detection without the O(n) List.Contains cost.
        var seenMandatory = new HashSet<ushort>();
        var seenOptional = new HashSet<ushort>();
        var seenManufacturer = new HashSet<ushort>();

        foreach (var objElem in objList.Elements().Where(e => e.Name.LocalName == "CANopenObject"))
        {
            var obj = ParseCanOpenObject(objElem, includeActualValues);
            dict.Objects[obj.Index] = obj;

            // Classify object into the right list based on index range
            ClassifyObject(dict, obj.Index, seenMandatory, seenOptional, seenManufacturer);
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

    private static void ClassifyObject(
        ObjectDictionary dict, ushort index,
        HashSet<ushort> seenMandatory, HashSet<ushort> seenOptional, HashSet<ushort> seenManufacturer)
    {
        // Mandatory objects: 1000h and 1001h
        if (index == 0x1000 || index == 0x1001)
        {
            if (seenMandatory.Add(index))
                dict.MandatoryObjects.Add(index);
        }
        // Manufacturer-specific objects: 2000h-5FFFh
        else if (index >= 0x2000 && index <= 0x5FFF)
        {
            if (seenManufacturer.Add(index))
                dict.ManufacturerObjects.Add(index);
        }
        // Everything else goes to optional
        else
        {
            if (seenOptional.Add(index))
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

            var keyPart = entry[..eqIdx].Trim();
            var valPart = entry[(eqIdx + 1)..].Trim();

            // keyPart must start with "Dummy" followed by 4 hex digits
            if (!keyPart.StartsWith("Dummy", StringComparison.OrdinalIgnoreCase) || keyPart.Length < 9)
                continue;

            var hexPart = keyPart[5..];
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
                parsed = byte.TryParse(nodeIdStr[2..], NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out nodeIdValue);
            else
                parsed = byte.TryParse(nodeIdStr, NumberStyles.None,
                    CultureInfo.InvariantCulture, out nodeIdValue);

            if (parsed)
            {
                if (nodeIdValue < 1 || nodeIdValue > 127)
                    throw new EdsParseException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Invalid nodeID '{0}' (parsed value {1}). CANopen Node-ID must be in range 1..127.",
                            nodeIdStr,
                            nodeIdValue));
                dc.NodeId = nodeIdValue;
            }
            else
            {
                throw new EdsParseException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Invalid nodeID '{0}'. Value cannot be parsed as a CANopen Node-ID (byte).",
                        nodeIdStr));
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

    private static void SetBaudRate(BaudRates baudRates, ushort kbps)
    {
        if (BaudRateSetters.TryGetValue(kbps, out var setter))
            setter(baudRates);
    }
}
