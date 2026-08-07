namespace EdsDcfNet.Parsers;

using System.Globalization;
using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;
using static EdsDcfNet.Parsers.XddParsingPrimitives;

internal static class XddDeviceProfileParser
{
    internal static EdsFileInfo ParseFileInfo(XElement profileBody)
    {
        var fileInfo = new EdsFileInfo();

        fileInfo.FileName = profileBody.Attribute("fileName")?.Value ?? string.Empty;
        fileInfo.CreatedBy = profileBody.Attribute("fileCreator")?.Value ?? string.Empty;
        fileInfo.ModifiedBy = profileBody.Attribute("fileModifiedBy")?.Value ?? string.Empty;

        // fileVersion is a string like "1.0", "1,0", or "1" — map to Unsigned8 FileVersion.
        // Lenient: take major from major/minor forms (same as EDS/DCF FileInfo).
        // StrictParsing: require a plain integer via ParseByte.
        var fileVersionStr = profileBody.Attribute("fileVersion")?.Value ?? string.Empty;
        if (!string.IsNullOrEmpty(fileVersionStr))
        {
            var trimmed = fileVersionStr.Trim();
            try
            {
                if (StrictParsingScope.IsEnabled)
                {
                    fileInfo.FileVersion = ValueConverter.ParseByte(trimmed);
                }
                else if (ValueConverter.TrySplitMajorMinorDecimal(trimmed, out var major))
                {
                    fileInfo.FileVersion = ValueConverter.ParseByte(major);
                }
                else if (byte.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var ver))
                {
                    // Preserve historical XDD lenient behavior for plain decimals only;
                    // hex/octal/invalid tokens keep the model default rather than throwing.
                    fileInfo.FileVersion = ver;
                }
            }
            catch (EdsParseException ex)
            {
                throw new EdsParseException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ProfileBody fileVersion: {0}",
                        ex.Message),
                    ex);
            }
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
