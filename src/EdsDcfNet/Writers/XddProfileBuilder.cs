namespace EdsDcfNet.Writers;

using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using EdsDcfNet.Models;

/// <summary>
/// Builds shared XDD profile structure elements: ISO 15745 profile wrappers,
/// file info attributes, device identity, and the static children of
/// <c>ApplicationLayers</c> (dummyUsage, dynamicChannels) and
/// <c>NetworkManagement</c> (CANopenGeneralFeatures, CANopenMasterFeatures).
/// </summary>
internal static class XddProfileBuilder
{
    private static readonly XName ApplicationLayersName =
        XddNames.ChildOfType(XddNames.NetworkProfileBodyType, "ApplicationLayers");

    private static readonly XName NetworkManagementName =
        XddNames.ChildOfType(XddNames.NetworkProfileBodyType, "NetworkManagement");

    // ── ISO 15745 profile wrapper ─────────────────────────────────────────────

    /// <summary>
    /// Wraps a <paramref name="profileBody"/> in a full <c>ISO15745Profile</c> element
    /// with a standard header for the given <paramref name="classId"/>.
    /// </summary>
    internal static XElement BuildProfile(string classId, XElement profileBody)
    {
        var profileName = XddNames.Child(XddNames.ProfileContainer, "ISO15745Profile");
        var headerName = XddNames.Child(profileName, "ProfileHeader");
        var referenceName = XddNames.Child(headerName, "ISO15745Reference");
        return new XElement(profileName,
            new XElement(headerName,
                XddNames.Element(headerName, "ProfileIdentification", string.Empty),
                XddNames.Element(headerName, "ProfileRevision", "1"),
                XddNames.Element(headerName, "ProfileName", string.Empty),
                XddNames.Element(headerName, "ProfileSource", string.Empty),
                XddNames.Element(headerName, "ProfileClassID", classId),
                new XElement(referenceName,
                    XddNames.Element(referenceName, "ISO15745Part", "1"),
                    XddNames.Element(referenceName, "ISO15745Edition", "1"),
                    XddNames.Element(referenceName, "ProfileTechnology", "CANopen"))),
            profileBody);
    }

    // ── ProfileBody file info ─────────────────────────────────────────────────

    /// <summary>Adds file metadata attributes to a <c>ProfileBody</c> element.</summary>
    /// <remarks>
    /// <c>fileName</c>, <c>fileCreator</c> and <c>fileVersion</c> are required and always written
    /// (an empty string is schema-valid). A date or time is written only when it is valid; an
    /// omitted optional attribute is schema-valid, a mistyped one is not. A missing or invalid
    /// <c>fileCreationDate</c> (required) is not invented; see <c>XmlWriteRules</c>.
    /// </remarks>
    internal static void AddFileInfoAttributes(XElement profileBody, EdsFileInfo fileInfo)
    {
        profileBody.Add(new XAttribute("fileName", fileInfo.FileName ?? string.Empty));
        profileBody.Add(new XAttribute("fileCreator", fileInfo.CreatedBy ?? string.Empty));

        if (XddFormatHelper.TryFormatFileDate(fileInfo.CreationDate, fileInfo.CreationDateLexical, out var creationDate))
            profileBody.Add(new XAttribute("fileCreationDate", creationDate));

        if (XddFormatHelper.TryConvertFileTimeToXsd(fileInfo.CreationTime, out var creationTime))
            profileBody.Add(new XAttribute("fileCreationTime", creationTime));

        profileBody.Add(new XAttribute("fileVersion", FileVersionAttribute(fileInfo)));

        if (XddFormatHelper.TryFormatFileDate(fileInfo.ModificationDate, fileInfo.ModificationDateLexical, out var modificationDate))
            profileBody.Add(new XAttribute("fileModificationDate", modificationDate));

        if (XddFormatHelper.TryConvertFileTimeToXsd(fileInfo.ModificationTime, out var modificationTime))
            profileBody.Add(new XAttribute("fileModificationTime", modificationTime));

        if (!string.IsNullOrEmpty(fileInfo.ModifiedBy))
            profileBody.Add(new XAttribute("fileModifiedBy", fileInfo.ModifiedBy));
    }

    // The read text stays while the number is unchanged since the read; a text without a baseline was
    // assigned by the caller. fileVersion is a free xsd:string.
    private static string FileVersionAttribute(EdsFileInfo fileInfo)
    {
        var number = fileInfo.FileVersion.ToString(CultureInfo.InvariantCulture);
        if (fileInfo.FileVersionText == null)
            return number;

        var baseline = fileInfo.FileVersionTextBaseline;
        return baseline.HasValue && baseline.Value != fileInfo.FileVersion ? number : fileInfo.FileVersionText;
    }

    // ── DeviceIdentity ────────────────────────────────────────────────────────

    /// <summary>Builds the <c>DeviceIdentity</c> element from <see cref="DeviceInfo"/>.</summary>
    /// <remarks>
    /// <see cref="DeviceInfo.OrderNumbers"/> and <see cref="DeviceInfo.Versions"/> are authoritative: a
    /// non-empty list is written as it is. Only an empty list falls back to
    /// <see cref="DeviceInfo.OrderCode"/> (when not empty) and <see cref="DeviceInfo.RevisionNumber"/>
    /// (as a firmware version). The schema order is <c>orderNumber</c> before <c>version</c>.
    /// </remarks>
    internal static XElement BuildDeviceIdentity(DeviceInfo deviceInfo)
    {
        var name = XddNames.ChildOfType(XddNames.DeviceProfileBodyType, "DeviceIdentity");
        var identity = new XElement(name,
            XddNames.Element(name, "vendorName", deviceInfo.VendorName),
            XddNames.Element(name, "vendorID",
                string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", deviceInfo.VendorNumber)),
            XddNames.Element(name, "productName", deviceInfo.ProductName),
            XddNames.Element(name, "productID",
                string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", deviceInfo.ProductNumber)));

        var orderNumbers = deviceInfo.OrderNumbers.Count > 0
            ? deviceInfo.OrderNumbers
            : OrderCodeAsList(deviceInfo.OrderCode);
        foreach (var orderNumber in orderNumbers)
            identity.Add(BuildReadOnlyText(name, "orderNumber", orderNumber.Value, orderNumber.ReadOnly));

        var versions = deviceInfo.Versions.Count > 0
            ? deviceInfo.Versions
            : new List<DeviceVersion>
            {
                new()
                {
                    Type = DeviceVersionType.Firmware,
                    Value = deviceInfo.RevisionNumber.ToString(CultureInfo.InvariantCulture)
                }
            };
        foreach (var version in versions)
        {
            var element = BuildReadOnlyText(name, "version", version.Value, version.ReadOnly);
            element.Add(new XAttribute("versionType", FormatVersionType(version.Type)));
            identity.Add(element);
        }

        return identity;
    }

    private static List<DeviceOrderNumber> OrderCodeAsList(string orderCode)
        => string.IsNullOrEmpty(orderCode)
            ? new List<DeviceOrderNumber>()
            : new List<DeviceOrderNumber> { new() { Value = orderCode } };

    // readOnly defaults to true in the schema, so only false is written.
    private static XElement BuildReadOnlyText(XName parent, string localName, string value, bool readOnly)
    {
        var element = XddNames.Element(parent, localName, value);
        if (!readOnly)
            element.Add(new XAttribute("readOnly", "false"));

        return element;
    }

    private static string FormatVersionType(DeviceVersionType type) => type switch
    {
        DeviceVersionType.Software => "SW",
        DeviceVersionType.Firmware => "FW",
        DeviceVersionType.Hardware => "HW",
        _ => throw new ArgumentOutOfRangeException(
            nameof(type),
            type,
            "DeviceVersion.Type is not a CiA 311 versionType (SW, FW or HW).")
    };

    // ── ApplicationLayers static children ────────────────────────────────────

    /// <summary>Builds the <c>dummyUsage</c> element from the object dictionary.</summary>
    internal static XElement BuildDummyUsage(ObjectDictionary dict)
    {
        var dummyElem = XddNames.Element(ApplicationLayersName, "dummyUsage");

        foreach (var kvp in dict.DummyUsage.OrderBy(d => d.Key))
        {
            dummyElem.Add(XddNames.Element(dummyElem.Name, "dummy",
                new XAttribute("entry",
                    string.Format(CultureInfo.InvariantCulture,
                        "Dummy{0:X4}={1}", kvp.Key, kvp.Value ? "1" : "0"))));
        }

        return dummyElem;
    }

    /// <summary>
    /// Builds the <c>dynamicChannels</c> element.
    /// </summary>
    /// <remarks>
    /// Every <c>dynamicChannel</c> carries the schema-required attributes
    /// <c>dataType</c>, <c>accessType</c>, <c>startIndex</c>, <c>endIndex</c>,
    /// <c>maxNumber</c>, and <c>addressOffset</c>. <c>bitAlignment</c> is written
    /// only when <see cref="DynamicChannelSegment.BitAlignment"/> is set.
    /// <c>pDOmappingIndex</c> is not part of the schema and is not written.
    /// </remarks>
    internal static XElement BuildDynamicChannels(DynamicChannels channels)
    {
        var dynElem = XddNames.Element(ApplicationLayersName, "dynamicChannels");

        foreach (var seg in channels.Segments)
        {
            var chanElem = XddNames.Element(dynElem.Name, "dynamicChannel",
                new XAttribute("dataType", XddFormatHelper.FormatDynamicChannelDataType(seg.Type)),
                new XAttribute("accessType", XddFormatHelper.DynamicChannelAccessTypeToString(seg.Dir)));

            AddDynamicChannelIndexes(chanElem, seg.Range);
            chanElem.Add(new XAttribute(
                "maxNumber",
                (seg.MaxNumber ?? DeriveMaxNumber(seg.Range)).ToString(CultureInfo.InvariantCulture)));
            chanElem.Add(new XAttribute("addressOffset", FormatAddressOffset(seg)));

            if (seg.BitAlignment.HasValue)
            {
                chanElem.Add(new XAttribute(
                    "bitAlignment",
                    seg.BitAlignment.Value.ToString(CultureInfo.InvariantCulture)));
            }

            dynElem.Add(chanElem);
        }

        return dynElem;
    }

    /// <summary>
    /// Writes <c>startIndex</c> and <c>endIndex</c>. A single index is repeated
    /// because <c>endIndex</c> is required. An unparsable range is written as <c>0000</c>.
    /// </summary>
    private static void AddDynamicChannelIndexes(XElement channel, string range)
    {
        SplitRange(range, out var startText, out var endText);
        var hasStart = TryParseHexIndex(startText, out var start);
        var hasEnd = TryParseHexIndex(endText, out var end);

        if (!hasStart)
        {
            channel.Add(new XAttribute("startIndex", XddFormatHelper.FormatHexBinary(0)));
            channel.Add(new XAttribute("endIndex", XddFormatHelper.FormatHexBinary(0)));
            return;
        }

        channel.Add(new XAttribute("startIndex", XddFormatHelper.FormatHexBinary(start)));
        channel.Add(new XAttribute("endIndex", XddFormatHelper.FormatHexBinary(hasEnd ? end : start)));
    }

    /// <summary>
    /// Inclusive index span of <paramref name="range"/>, <c>1</c> for a single index,
    /// or <c>0</c> when the range cannot be parsed or the end index is below the start.
    /// </summary>
    private static uint DeriveMaxNumber(string range)
    {
        SplitRange(range, out var startText, out var endText);
        if (!TryParseHexIndex(startText, out var start))
            return 0;
        if (!TryParseHexIndex(endText, out var end))
            return 1;
        if (end < start)
            return 0;

        var span = end - start;
        if (span == uint.MaxValue)
            return uint.MaxValue;

        return span + 1;
    }

    private static string FormatAddressOffset(DynamicChannelSegment segment)
    {
        if (segment.AddressOffsetLexical != null &&
            segment.PPOffset == segment.AddressOffsetLexicalBaseline)
        {
            return segment.AddressOffsetLexical;
        }

        return XddFormatHelper.FormatHexBinary(segment.PPOffset);
    }

    private static void SplitRange(string range, out string start, out string end)
    {
        var hyphen = range.IndexOf('-');
        if (hyphen < 0)
        {
            start = range.Trim();
            end = string.Empty;
            return;
        }

        start = range.Substring(0, hyphen).Trim();
        end = range.Substring(hyphen + 1).Trim();
    }

    /// <summary>
    /// Parses one side of a <see cref="DynamicChannelSegment.Range"/> as hexadecimal
    /// with an optional <c>0x</c> prefix. <see cref="SplitRange"/> has already removed
    /// surrounding whitespace; interior whitespace is not a valid index.
    /// </summary>
    private static bool TryParseHexIndex(string text, out uint value)
    {
        value = 0;
        if (string.IsNullOrEmpty(text))
            return false;

        var hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? text.Substring(2)
            : text;

        if (hex.Length == 0)
            return false;

        return uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    // ── NetworkManagement static children ─────────────────────────────────────

    /// <summary>Builds the <c>CANopenGeneralFeatures</c> element.</summary>
    internal static XElement BuildGeneralFeatures(DeviceInfo deviceInfo)
    {
        var features = XddNames.Element(NetworkManagementName, "CANopenGeneralFeatures",
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
        AddTrueFlag(features, "selfStartingDevice", deviceInfo.SelfStartingDevice);
        AddTrueFlag(features, "SDORequestingDevice", deviceInfo.SdoRequestingDevice);
        return features;
    }

    // These attributes default to false in the schema, so only true is written.
    private static void AddTrueFlag(XElement element, string attribute, bool value)
    {
        if (value)
            element.Add(new XAttribute(attribute, "true"));
    }

    /// <summary>Builds the <c>CANopenMasterFeatures</c> element.</summary>
    internal static XElement BuildMasterFeatures(DeviceInfo deviceInfo)
    {
        var features = XddNames.Element(NetworkManagementName, "CANopenMasterFeatures",
            new XAttribute("bootUpMaster",
                deviceInfo.SimpleBootUpMaster ? "true" : "false"));
        AddTrueFlag(features, "flyingMaster", deviceInfo.FlyingMaster);
        AddTrueFlag(features, "SDOManager", deviceInfo.SdoManager);
        AddTrueFlag(features, "configurationManager", deviceInfo.ConfigurationManager);
        AddTrueFlag(features, "layerSettingServiceMaster", deviceInfo.LayerSettingServiceMaster);
        return features;
    }
}
