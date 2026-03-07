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
    // ── ISO 15745 profile wrapper ─────────────────────────────────────────────

    /// <summary>
    /// Wraps a <paramref name="profileBody"/> in a full <c>ISO15745Profile</c> element
    /// with a standard header for the given <paramref name="classId"/>.
    /// </summary>
    internal static XElement BuildProfile(string classId, XElement profileBody)
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

    // ── ProfileBody file info ─────────────────────────────────────────────────

    /// <summary>Adds file metadata attributes to a <c>ProfileBody</c> element.</summary>
    internal static void AddFileInfoAttributes(XElement profileBody, EdsFileInfo fileInfo)
    {
        if (!string.IsNullOrEmpty(fileInfo.FileName))
            profileBody.Add(new XAttribute("fileName", fileInfo.FileName));

        if (!string.IsNullOrEmpty(fileInfo.CreatedBy))
            profileBody.Add(new XAttribute("fileCreator", fileInfo.CreatedBy));

        // Convert EDS date "MM-DD-YYYY" to XSD date "YYYY-MM-DD"
        var xsdCreationDate = XddFormatHelper.ConvertEdsDateToXsd(fileInfo.CreationDate);
        if (!string.IsNullOrEmpty(xsdCreationDate))
            profileBody.Add(new XAttribute("fileCreationDate", xsdCreationDate));

        if (!string.IsNullOrEmpty(fileInfo.CreationTime))
            profileBody.Add(new XAttribute("fileCreationTime", fileInfo.CreationTime));

        profileBody.Add(new XAttribute("fileVersion",
            fileInfo.FileVersion.ToString(CultureInfo.InvariantCulture)));

        var xsdModDate = XddFormatHelper.ConvertEdsDateToXsd(fileInfo.ModificationDate);
        if (!string.IsNullOrEmpty(xsdModDate))
            profileBody.Add(new XAttribute("fileModificationDate", xsdModDate));

        if (!string.IsNullOrEmpty(fileInfo.ModificationTime))
            profileBody.Add(new XAttribute("fileModificationTime", fileInfo.ModificationTime));

        if (!string.IsNullOrEmpty(fileInfo.ModifiedBy))
            profileBody.Add(new XAttribute("fileModifiedBy", fileInfo.ModifiedBy));
    }

    // ── DeviceIdentity ────────────────────────────────────────────────────────

    /// <summary>Builds the <c>DeviceIdentity</c> element from <see cref="DeviceInfo"/>.</summary>
    internal static XElement BuildDeviceIdentity(DeviceInfo deviceInfo)
    {
        return new XElement("DeviceIdentity",
            new XElement("vendorName", deviceInfo.VendorName),
            new XElement("vendorID",
                string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", deviceInfo.VendorNumber)),
            new XElement("productName", deviceInfo.ProductName),
            new XElement("productID",
                string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", deviceInfo.ProductNumber)));
    }

    // ── ApplicationLayers static children ────────────────────────────────────

    /// <summary>Builds the <c>dummyUsage</c> element from the object dictionary.</summary>
    internal static XElement BuildDummyUsage(ObjectDictionary dict)
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

    /// <summary>Builds the <c>dynamicChannels</c> element.</summary>
    internal static XElement BuildDynamicChannels(DynamicChannels channels)
    {
        var dynElem = new XElement("dynamicChannels");

        foreach (var seg in channels.Segments)
        {
            var chanElem = new XElement("dynamicChannel",
                new XAttribute("dataType", XddFormatHelper.FormatDataType(seg.Type)),
                new XAttribute("accessType", XddFormatHelper.AccessTypeToString(seg.Dir)));

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

    // ── NetworkManagement static children ─────────────────────────────────────

    /// <summary>Builds the <c>CANopenGeneralFeatures</c> element.</summary>
    internal static XElement BuildGeneralFeatures(DeviceInfo deviceInfo)
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

    /// <summary>Builds the <c>CANopenMasterFeatures</c> element.</summary>
    internal static XElement BuildMasterFeatures(DeviceInfo deviceInfo)
    {
        return new XElement("CANopenMasterFeatures",
            new XAttribute("bootUpMaster",
                deviceInfo.SimpleBootUpMaster ? "true" : "false"));
    }
}
