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

        // fileVersion is an xsd:string (CiA 311 Annex A.1.2), FileVersion an Unsigned8. A plain decimal
        // number (zero-padded "010" stays decimal 10, never CiA octal) sets the property. Anything
        // else is kept as text in FileVersionText: the lenient major/minor tooling form ("1.0",
        // "1,0") also sets the major component, other text leaves the default and is reported. Both
        // are valid xsd:string input, so StrictParsing does not reject them. Whitespace-only matches a missing
        // attribute and keeps the model default (1).
        var fileVersionText = profileBody.Attribute("fileVersion")?.Value;
        if (fileVersionText != null)
            ReadFileVersion(fileInfo, fileVersionText, fileVersionText.Trim());

        // fileCreationDate is xsd:date "YYYY-MM-DD" → convert to EDS "MM-DD-YYYY". The original
        // spelling (it may carry a time zone) is kept so the writer can emit it unchanged.
        var creationDate = profileBody.Attribute("fileCreationDate")?.Value ?? string.Empty;
        fileInfo.CreationDate = ConvertXsdDateToEds(creationDate);
        fileInfo.CreationDateLexical = PreservedDateSpelling(creationDate);

        var creationTime = profileBody.Attribute("fileCreationTime")?.Value ?? string.Empty;
        fileInfo.CreationTime = creationTime;

        var modDate = profileBody.Attribute("fileModificationDate")?.Value ?? string.Empty;
        fileInfo.ModificationDate = ConvertXsdDateToEds(modDate);
        fileInfo.ModificationDateLexical = PreservedDateSpelling(modDate);

        var modTime = profileBody.Attribute("fileModificationTime")?.Value ?? string.Empty;
        fileInfo.ModificationTime = modTime;

        return fileInfo;
    }

    private static string? PreservedDateSpelling(string raw)
    {
        var trimmed = raw.Trim();
        return Writers.XddFormatHelper.IsXsdDate(trimmed) ? trimmed : null;
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

        // Both lists are kept whole (value and readOnly, in file order). The XDD/XDC writer writes
        // them back; OrderCode only mirrors the first order number for EDS and DCF.
        foreach (var element in identity.Elements().Where(e => e.Name.LocalName == "orderNumber"))
        {
            deviceInfo.OrderNumbers.Add(new DeviceOrderNumber
            {
                Value = element.Value,
                ReadOnly = ReadReadOnly(element)
            });
        }

        if (deviceInfo.OrderNumbers.Count > 0)
            deviceInfo.OrderCode = deviceInfo.OrderNumbers[0].Value.Trim();

        foreach (var element in identity.Elements().Where(e => e.Name.LocalName == "version"))
        {
            var type = ReadVersionType(element);
            if (type.HasValue)
            {
                deviceInfo.Versions.Add(new DeviceVersion
                {
                    Type = type.Value,
                    Value = element.Value,
                    ReadOnly = ReadReadOnly(element)
                });
            }
        }

        return deviceInfo;
    }

    // readOnly is xsd:boolean with the default true.
    private static bool ReadReadOnly(XElement element)
        => element.Attribute("readOnly")?.Value is not string readOnly || ParseXmlBool(readOnly);

    // versionType is required and one of SW, FW, HW. A missing or unknown value is not a CiA 311
    // version: it is reported and the element is not kept (strict mode rejects it).
    private static DeviceVersionType? ReadVersionType(XElement element)
    {
        var raw = element.Attribute("versionType")?.Value;
        var token = raw?.Trim();
        switch (token)
        {
            case "SW":
                return DeviceVersionType.Software;
            case "FW":
                return DeviceVersionType.Firmware;
            case "HW":
                return DeviceVersionType.Hardware;
        }

        var message = string.Format(
            CultureInfo.InvariantCulture,
            "DeviceIdentity version has versionType '{0}'; expected SW, FW or HW. The element is ignored.",
            raw ?? "(missing)");
        if (StrictParsingScope.IsEnabled)
        {
            throw new EdsParseException(message)
            {
                Code = Diagnostics.ParseDiagnosticCodes.XddUnknownVersionType
            };
        }

        Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
            Diagnostics.ParseSeverity.Warning,
            Diagnostics.ParseDiagnosticCodes.XddUnknownVersionType,
            path: "DeviceIdentity/version/@versionType",
            rawValue: raw,
            message: message));
        return null;
    }

    private static void ReadFileVersion(EdsFileInfo fileInfo, string text, string trimmed)
    {
        byte version;
        if (ValueConverter.TrySplitMajorMinorDecimal(trimmed, out var major))
        {
            // Leading-zero majors stay decimal (e.g. "012.5" → 12).
            if (byte.TryParse(major, NumberStyles.None, CultureInfo.InvariantCulture, out version))
            {
                Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
                    Diagnostics.ParseSeverity.Warning,
                    Diagnostics.ParseDiagnosticCodes.XddFileVersionMajorMinor,
                    path: "ProfileBody.fileVersion",
                    rawValue: trimmed,
                    coercedTo: major,
                    message: string.Format(
                        CultureInfo.InvariantCulture,
                        "fileVersion '{0}' uses a major/minor tooling form; the major component {1} is used.",
                        trimmed,
                        major)));
                fileInfo.FileVersion = version;
                KeepFileVersionText(fileInfo, text);
                return;
            }
        }
        else if (byte.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out version))
        {
            fileInfo.FileVersion = version;
            return;
        }

        Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
            Diagnostics.ParseSeverity.Warning,
            Diagnostics.ParseDiagnosticCodes.XddFileVersionNotNumeric,
            path: "ProfileBody.fileVersion",
            rawValue: trimmed,
            coercedTo: fileInfo.FileVersion.ToString(CultureInfo.InvariantCulture),
            message: string.Format(
                CultureInfo.InvariantCulture,
                "fileVersion '{0}' holds no number from 0 to 255; the text is preserved and FileVersion keeps {1}.",
                trimmed,
                fileInfo.FileVersion)));
        KeepFileVersionText(fileInfo, text);
    }

    private static void KeepFileVersionText(EdsFileInfo fileInfo, string text)
    {
        fileInfo.FileVersionText = text;
        fileInfo.FileVersionTextBaseline = fileInfo.FileVersion;
    }
}
