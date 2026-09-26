namespace EdsDcfNet.Checker;

using System.Globalization;
using System.Text.RegularExpressions;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Utilities;

/// <summary>
/// Checks the mandatory sections and entries of CiA 306-1 (<c>[FileInfo]</c>,
/// <c>[DeviceInfo]</c>, object lists, DCF <c>[DeviceComissioning]</c>) and the format of
/// their values. CiA 306-1: "If not otherwise specified, all sections and entries in this
/// document are mandatory."
/// </summary>
public sealed class MandatoryFieldsChecker
{
    private static readonly Regex TimePattern = new(
        "^(0[0-9]|1[0-2]):[0-5][0-9](AM|PM)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex DatePattern = new(
        "^(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])-[0-9]{4}$", RegexOptions.CultureInvariant);

    private static readonly Regex EdsVersionPattern = new("^[0-9]\\.[0-9]$", RegexOptions.CultureInvariant);

    private static readonly string[] BaudRateKeys =
    {
        "BaudRate_10", "BaudRate_20", "BaudRate_50", "BaudRate_125",
        "BaudRate_250", "BaudRate_500", "BaudRate_800", "BaudRate_1000",
    };

    private static readonly HashSet<uint> AllowedBaudrates = new() { 10, 20, 50, 125, 250, 500, 800, 1000 };

    private readonly string _file;
    private readonly RawIniDocument _doc;
    private readonly bool _isDcf;
    private readonly List<Finding> _findings;

    public MandatoryFieldsChecker(string file, RawIniDocument doc, bool isDcf, List<Finding> findings)
    {
        _file = file;
        _doc = doc;
        _isDcf = isDcf;
        _findings = findings;
    }

    public void Run()
    {
        CheckFileInfo();
        CheckDeviceInfo();

        if (_doc.Get("MandatoryObjects") is null)
        {
            _findings.Add(new Finding(Severity.Error, "MND001", _file, null, "MandatoryObjects", null, null,
                "Mandatory section is missing."));
        }

        if (_isDcf)
        {
            CheckDeviceCommissioning();
        }
    }

    private void CheckFileInfo()
    {
        var section = RequireSection("FileInfo");
        if (section is null)
        {
            return;
        }

        RequireText(section, "FileName", maxLength: null);
        RequireVersionByte(section, "FileVersion");
        RequireVersionByte(section, "FileRevision");

        var edsVersion = section.Get("EDSVersion");
        if (edsVersion is null || string.IsNullOrWhiteSpace(edsVersion.Value))
        {
            Add(Severity.Warning, "MND002", section, edsVersion, "EDSVersion",
                "EDSVersion is missing; it is then interpreted as \"3.0\". Files according to CiA 306 shall use \"4.0\".");
        }
        else if (!EdsVersionPattern.IsMatch(edsVersion.Value))
        {
            Add(Severity.Error, "MND003", section, edsVersion, edsVersion.Key, "EDSVersion must have the format \"X.Y\" (e.g. 4.0).");
        }

        RequireText(section, "Description", 243);
        RequirePattern(section, "CreationTime", TimePattern, "hh:mm(AM|PM)");
        RequirePattern(section, "CreationDate", DatePattern, "mm-dd-yyyy");
        RequireText(section, "CreatedBy", 245);
        RequirePattern(section, "ModificationTime", TimePattern, "hh:mm(AM|PM)");
        RequirePattern(section, "ModificationDate", DatePattern, "mm-dd-yyyy");
        RequireText(section, "ModifiedBy", 244);
    }

    private void CheckDeviceInfo()
    {
        var section = RequireSection("DeviceInfo");
        if (section is null)
        {
            return;
        }

        RequireText(section, "VendorName", 244);
        var vendorNumber = RequireUnsigned(section, "VendorNumber", 32);
        RequireText(section, "ProductName", 243);
        var productNumber = RequireUnsigned(section, "ProductNumber", 32);
        var revisionNumber = RequireUnsigned(section, "RevisionNumber", 32);
        RequireText(section, "OrderCode", 245);

        var anyBaudrate = false;
        foreach (var key in BaudRateKeys)
        {
            anyBaudrate |= RequireBoolean(section, key) == true;
        }

        if (!anyBaudrate)
        {
            Add(Severity.Warning, "MND004", section, null, null, "No BaudRate_xxx entry is set to 1; the device supports no bit rate.");
        }

        RequireBoolean(section, "SimpleBootUpMaster");
        RequireBoolean(section, "SimpleBootUpSlave");

        var granularity = RequireUnsigned(section, "Granularity", 8);
        if (granularity is > 64)
        {
            Add(Severity.Error, "MND003", section, section.Get("Granularity"), "Granularity",
                "Granularity must be 0 (mapping not modifiable) or 1..64.");
        }

        RequireUnsigned(section, "DynamicChannelsSupported", 8);
        RequireBoolean(section, "GroupMessaging");
        var rxPdos = RequireUnsigned(section, "NrOfRxPDO", 16);
        var txPdos = RequireUnsigned(section, "NrOfTxPDO", 16);
        RequireBoolean(section, "LSS_Supported");

        CompareWithIdentity(section, "VendorNumber", vendorNumber, 1);
        CompareWithIdentity(section, "ProductNumber", productNumber, 2);
        CompareWithIdentity(section, "RevisionNumber", revisionNumber, 3);

        // Explicitly described PDOs must not exceed the announced number (CompactPDO may add implicit ones).
        ComparePdoCount(section, "NrOfRxPDO", rxPdos, 0x1400);
        ComparePdoCount(section, "NrOfTxPDO", txPdos, 0x1800);
    }

    private void CheckDeviceCommissioning()
    {
        var section = _doc.Get("DeviceComissioning") ?? _doc.Get("DeviceCommissioning");
        if (section is null)
        {
            _findings.Add(new Finding(Severity.Error, "MND001", _file, null, "DeviceComissioning", null, null,
                "Mandatory DCF section is missing."));
            return;
        }

        if (section.Name.Equals("DeviceCommissioning", StringComparison.OrdinalIgnoreCase))
        {
            Add(Severity.Warning, "MND003", section, null, null,
                "CiA 306 spells the section [DeviceComissioning] (single 'm'); other tools may not find [DeviceCommissioning].");
        }

        // NodeID itself is checked by RawObjectChecker (DCF001/DCF002).
        RequireText(section, "NodeName", 246);
        var baudrate = RequireUnsigned(section, "Baudrate", 16);
        if (baudrate.HasValue && !AllowedBaudrates.Contains((uint)baudrate.Value))
        {
            Add(Severity.Error, "MND003", section, section.Get("Baudrate"), "Baudrate",
                "Baudrate must be one of 10, 20, 50, 125, 250, 500, 800, 1000 (kbit/s).");
        }

        RequireUnsigned(section, "NetNumber", 32);
        RequireText(section, "NetworkName", 243);

        // CANopenManager: "0" or missing = not the manager.
        var manager = section.Get("CANopenManager");
        if (manager is not null && !string.IsNullOrWhiteSpace(manager.Value) && manager.Value.Trim() is not ("0" or "1"))
        {
            Add(Severity.Error, "MND003", section, manager, manager.Key, "CANopenManager is a BOOLEAN; valid values are \"0\" and \"1\".");
        }
    }

    // ---------------------------------------------------------------- cross checks

    private void CompareWithIdentity(RawSection section, string key, ulong? value, byte sub)
    {
        if (!value.HasValue)
        {
            return;
        }

        var identity = FindIdentitySubSection(sub);
        // DCF stores the commissioned identity in ParameterValue; EDS (and a DCF
        // without that entry) keeps it in DefaultValue. Section names are often
        // zero-padded ([1018sub01]) as well as unpadded ([1018sub1]).
        var identityKey = _isDcf && identity?.GetValue("ParameterValue") is not null ? "ParameterValue" : "DefaultValue";
        var identityValue = identity?.GetValue(identityKey);
        if (identity is null || identityValue is null || identityValue.Contains('$'))
        {
            return;
        }

        try
        {
            var expected = ValueConverter.ParseInteger(identityValue);
            if (expected != value.Value)
            {
                Add(Severity.Warning, "MND005", section, section.Get(key), key, string.Format(CultureInfo.InvariantCulture,
                    "{0}=0x{1:X} differs from [{2}] {3}=0x{4:X}.", key, value.Value, identity.Name, identityKey, expected));
            }
        }
        catch (Exception ex) when (ex is EdsParseException or FormatException or OverflowException)
        {
            // reported by the value checks
        }
    }

    private RawSection? FindIdentitySubSection(byte sub)
    {
        var unpadded = string.Format(CultureInfo.InvariantCulture, "1018sub{0:X}", sub);
        var padded = string.Format(CultureInfo.InvariantCulture, "1018sub{0:X2}", sub);
        return _doc.Get(unpadded) ?? _doc.Get(padded);
    }

    private void ComparePdoCount(RawSection section, string key, ulong? announced, ushort baseIndex)
    {
        if (!announced.HasValue)
        {
            return;
        }

        var described = 0;
        for (var index = baseIndex; index < baseIndex + 0x200; index++)
        {
            if (_doc.Get(((ushort)index).ToString("X4", CultureInfo.InvariantCulture)) is not null)
            {
                described++;
            }
        }

        if ((ulong)described > announced.Value)
        {
            Add(Severity.Error, "MND005", section, section.Get(key), key, string.Format(CultureInfo.InvariantCulture,
                "{0}={1}, but {2} PDO communication parameter objects (0x{3:X4}..) are described.", key, announced.Value, described, baseIndex));
        }
    }

    // ---------------------------------------------------------------- primitives

    private RawSection? RequireSection(string name)
    {
        var section = _doc.Get(name);
        if (section is null)
        {
            _findings.Add(new Finding(Severity.Error, "MND001", _file, null, name, null, null, "Mandatory section is missing."));
        }

        return section;
    }

    private RawEntry? RequireEntry(RawSection section, string key)
    {
        var entry = section.Get(key);
        if (entry is null)
        {
            Add(Severity.Warning, "MND002", section, null, key, "Mandatory entry " + key + " is missing.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(entry.Value))
        {
            Add(Severity.Warning, "MND002", section, entry, key, "Mandatory entry " + key + " is empty.");
            return null;
        }

        return entry;
    }

    private void RequireText(RawSection section, string key, int? maxLength)
    {
        var entry = RequireEntry(section, key);
        if (entry is not null && maxLength.HasValue && entry.Value.Length > maxLength.Value)
        {
            Add(Severity.Error, "MND003", section, entry, key, string.Format(CultureInfo.InvariantCulture,
                "{0} has {1} characters; CiA 306 allows at most {2}.", key, entry.Value.Length, maxLength.Value));
        }
    }

    private void RequirePattern(RawSection section, string key, Regex pattern, string format)
    {
        var entry = RequireEntry(section, key);
        if (entry is not null && !pattern.IsMatch(entry.Value))
        {
            Add(Severity.Warning, "MND003", section, entry, key, key + " must have the format \"" + format + "\".");
        }
    }

    /// <summary>
    /// FileVersion/FileRevision are plain decimal UNSIGNED8 values for the EdsDcfNet reader
    /// (<c>08</c> is 8 and <c>010</c> is 10, no CiA octal). A genuine major/minor form such as
    /// <c>1.0</c> or <c>1,0</c> is a warning: the lenient reader keeps the major part.
    /// Hex and signed literals (<c>0x01</c>, <c>+1</c>) are not that form; the reader rejects them.
    /// </summary>
    private void RequireVersionByte(RawSection section, string key)
    {
        var entry = RequireEntry(section, key);
        if (entry is null)
        {
            return;
        }

        try
        {
            ValueConverter.ParseByteDecimalPlain(entry.Value);
            return;
        }
        catch (EdsParseException)
        {
            // fall through: either a tooling major/minor form, or a literal the reader cannot load
        }

        if (TrySplitToolingMajorMinor(entry.Value, out var majorText))
        {
            try
            {
                var major = byte.Parse(majorText, NumberStyles.None, CultureInfo.InvariantCulture);
                Add(Severity.Warning, "MND003", section, entry, key, string.Format(CultureInfo.InvariantCulture,
                    "{0} should be a decimal UNSIGNED8 number; the major/minor form is read as {1} by lenient readers.", key, major));
            }
            catch (OverflowException)
            {
                Add(Severity.Error, "MND003", section, entry, key, key + " must be a decimal UNSIGNED8 number (0..255).");
            }

            return;
        }

        Add(Severity.Error, "MND003", section, entry, key, key + " must be a decimal UNSIGNED8 number (0..255).");
    }

    /// <summary>
    /// Matches <c>ValueConverter.TrySplitMajorMinorDecimal</c>: one <c>.</c> or <c>,</c>
    /// between two non-empty ASCII digit runs. Hex prefixes are numeric literals, not versions.
    /// </summary>
    private static bool TrySplitToolingMajorMinor(string value, out string major)
    {
        major = string.Empty;
        value = value.Trim();

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var separatorIndex = -1;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c != '.' && c != ',')
            {
                continue;
            }

            if (separatorIndex >= 0)
            {
                return false;
            }

            separatorIndex = i;
        }

        if (separatorIndex <= 0 || separatorIndex >= value.Length - 1)
        {
            return false;
        }

        var majorPart = value[..separatorIndex].Trim();
        var minorPart = value[(separatorIndex + 1)..].Trim();
        if (majorPart.Length == 0 || minorPart.Length == 0)
        {
            return false;
        }

        if (!IsAllAsciiDigits(majorPart) || !IsAllAsciiDigits(minorPart))
        {
            return false;
        }

        major = majorPart;
        return true;
    }

    private static bool IsAllAsciiDigits(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c < '0' || c > '9')
            {
                return false;
            }
        }

        return true;
    }

    private ulong? RequireUnsigned(RawSection section, string key, int bits)
    {
        var entry = RequireEntry(section, key);
        if (entry is null)
        {
            return null;
        }

        var dataType = bits switch
        {
            8 => CanOpenDataType.Unsigned8,
            16 => CanOpenDataType.Unsigned16,
            _ => CanOpenDataType.Unsigned32,
        };

        try
        {
            return Convert.ToUInt64(CanOpenValueConverter.Parse(entry.Value, dataType), CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or EdsParseException or NotSupportedException)
        {
            Add(Severity.Error, "MND003", section, entry, key, key + " must be a " + ValueSupport.RangeDescription(dataType) + " number.");
            return null;
        }
    }

    private bool? RequireBoolean(RawSection section, string key)
    {
        var entry = RequireEntry(section, key);
        if (entry is null)
        {
            return null;
        }

        switch (entry.Value.Trim())
        {
            case "0": return false;
            case "1": return true;
            default:
                Add(Severity.Error, "MND003", section, entry, key, key + " is a BOOLEAN; valid values are \"0\" and \"1\".");
                return null;
        }
    }

    private void Add(Severity severity, string code, RawSection section, RawEntry? entry, string? key, string message) =>
        _findings.Add(new Finding(severity, code, _file, entry?.Line ?? section.Line, section.Name, key, entry?.Value, message));
}
