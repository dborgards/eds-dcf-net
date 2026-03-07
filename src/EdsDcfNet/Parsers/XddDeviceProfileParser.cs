namespace EdsDcfNet.Parsers;

using System.Globalization;
using System.Xml.Linq;
using EdsDcfNet.Models;
using static EdsDcfNet.Parsers.XddParsingPrimitives;

internal static class XddDeviceProfileParser
{
    internal static EdsFileInfo ParseFileInfo(XElement profileBody)
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
            var majorPart = dotIdx >= 0 ? fileVersionStr[..dotIdx] : fileVersionStr;
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

    internal static DeviceInfo ParseDeviceIdentity(XElement profileBody)
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
}
