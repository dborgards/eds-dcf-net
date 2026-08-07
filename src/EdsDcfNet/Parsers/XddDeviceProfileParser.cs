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
        // Lenient: accept major/minor tooling forms; plain decimals via NumberStyles.None
        // (historical XDD path, so "010" stays decimal 10 rather than CiA octal).
        // StrictParsing: require a plain integer via ParseByte.
        // Invalid tokens throw in both modes (with ProfileBody attribution).
        // Trim before the empty check so whitespace-only attributes match missing/empty
        // and keep the model default (1), rather than ParseByte("") → 0 or throwing.
        var fileVersionStr = (profileBody.Attribute("fileVersion")?.Value ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(fileVersionStr))
        {
            try
            {
                if (StrictParsingScope.IsEnabled)
                {
                    fileInfo.FileVersion = ValueConverter.ParseByte(fileVersionStr);
                }
                else if (ValueConverter.TrySplitMajorMinorDecimal(fileVersionStr, out _))
                {
                    // Reuse ParseByteAllowingMajorMinor so leading-zero majors stay decimal
                    // (e.g. "012.5" → 12), matching EDS/DCF FileInfo policy.
                    fileInfo.FileVersion = ValueConverter.ParseByteAllowingMajorMinor(fileVersionStr);
                }
                else if (byte.TryParse(fileVersionStr, NumberStyles.None, CultureInfo.InvariantCulture, out var ver))
                {
                    fileInfo.FileVersion = ver;
                }
                else
                {
                    throw new EdsParseException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Invalid byte value: '{0}'.",
                            fileVersionStr));
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
