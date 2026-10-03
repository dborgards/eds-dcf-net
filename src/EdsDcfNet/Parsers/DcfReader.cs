namespace EdsDcfNet.Parsers;

using System.Globalization;

using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Reader for Device Configuration File (DCF) files.
/// DCF files extend EDS files with configured values and device-specific settings.
/// </summary>
public class DcfReader : CanOpenReaderBase, IFileReader<DeviceConfigurationFile>
{
    private static readonly string[] DcfKnownSectionNames =
    {
        "FileInfo", "DeviceInfo", "DeviceCommissioning", "DeviceComissioning", "DummyUsage",
        "MandatoryObjects", "OptionalObjects", "ManufacturerObjects",
        "Comments", "SupportedModules", "ConnectedModules", "Tools",
        "DynamicChannels"
    };

    /// <inheritdoc/>
    protected override string[] KnownSectionNames => DcfKnownSectionNames;

    /// <summary>
    /// Reads a DCF file from the specified path.
    /// </summary>
    /// <param name="filePath">Path to the DCF file</param>
    /// <param name="maxInputSize">Maximum file size in bytes.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public DeviceConfigurationFile ReadFile(
        string filePath,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
    {
        var sections = IniParser.ParseFile(filePath, maxInputSize);
        return ParseDcf(sections);
    }

    /// <summary>
    /// Reads a DCF file from a stream.
    /// </summary>
    /// <param name="stream">Readable stream containing DCF content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public DeviceConfigurationFile ReadStream(
        Stream stream,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
    {
        var sections = IniParser.ParseStream(stream, maxInputSize);
        return ParseDcf(sections);
    }

    /// <summary>
    /// Reads a DCF file from the specified path asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the DCF file</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public Task<DeviceConfigurationFile> ReadFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => ReadFileAsync(filePath, ReaderDefaults.DefaultMaxInputSize, cancellationToken);

    /// <summary>
    /// Reads a DCF file from the specified path asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the DCF file</param>
    /// <param name="maxInputSize">Maximum file size in bytes.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public async Task<DeviceConfigurationFile> ReadFileAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var sections = await IniParser.ParseFileAsync(filePath, maxInputSize, cancellationToken).ConfigureAwait(false);
        return ParseDcf(sections);
    }

    /// <summary>
    /// Reads a DCF file from a stream asynchronously.
    /// </summary>
    /// <param name="stream">Readable stream containing DCF content.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public Task<DeviceConfigurationFile> ReadStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => ReadStreamAsync(stream, ReaderDefaults.DefaultMaxInputSize, cancellationToken);

    /// <summary>
    /// Reads a DCF file from a stream asynchronously.
    /// </summary>
    /// <param name="stream">Readable stream containing DCF content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public async Task<DeviceConfigurationFile> ReadStreamAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var sections = await IniParser.ParseStreamAsync(stream, maxInputSize, cancellationToken).ConfigureAwait(false);
        return ParseDcf(sections);
    }

    /// <summary>
    /// Reads a DCF from a string.
    /// </summary>
    /// <param name="content">DCF file content as string</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters.</param>
    /// <returns>Parsed DeviceConfigurationFile object</returns>
    public DeviceConfigurationFile ReadString(
        string content,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
    {
        var sections = IniParser.ParseString(content, maxInputSize);
        return ParseDcf(sections);
    }

    private DeviceConfigurationFile ParseDcf(Dictionary<string, Dictionary<string, string>> sections)
    {
        var dcf = new DeviceConfigurationFile();
        ParseCommonSections(dcf, sections);
        return dcf;
    }

    #region DCF-specific overrides

    /// <inheritdoc/>
    private protected override void ParsePreObjectDictionarySections(
        ICanOpenFileModel model,
        Dictionary<string, Dictionary<string, string>> sections)
    {
        var dcf = (DeviceConfigurationFile)model;
        dcf.DeviceCommissioning = ParseDeviceCommissioning(dcf, sections);
    }

    /// <inheritdoc/>
    private protected override void ParsePostObjectDictionarySections(
        ICanOpenFileModel model,
        Dictionary<string, Dictionary<string, string>> sections)
    {
        ((DeviceConfigurationFile)model).ConnectedModules.AddRange(
            ParseConnectedModules(sections, model.SectionRemainingEntries));
    }

    /// <inheritdoc/>
    private protected override bool IsSectionHandledByFormat(string sectionName, ICanOpenFileModel model)
        => base.IsSectionHandledByFormat(sectionName, model)
           || ObjectLinksSectionHelper.IsObjectLinksSectionForExistingObject(sectionName, model.ObjectDictionary);

    /// <inheritdoc/>
    private protected override bool TryParseObjectCompanionSectionName(string sectionName, out ushort index)
        => base.TryParseObjectCompanionSectionName(sectionName, out index)
           || TryParseCompanionSuffixSection(sectionName, "Value", out index)
           || TryParseCompanionSuffixSection(sectionName, "Denotation", out index);

    /// <inheritdoc/>
    private protected override bool IsKnownFileInfoEntryKey(string key) => SectionEntryKeys.IsDcfFileInfoKey(key);

    /// <inheritdoc/>
    private protected override void CaptureObjectCompanionEntries(
        Dictionary<string, Dictionary<string, string>> sections,
        CanOpenObject obj,
        Dictionary<string, OrderedStringDictionary> store)
    {
        // ParseSubObjects applies [xxxxValue] / [xxxxDenotation] to every parsed object.
        foreach (var suffix in CompactValueSectionSuffixes)
        {
            var sectionName = string.Concat(ToHexInvariant(obj.Index), suffix);
            CanOpenSectionParsers.CaptureCompactListEntries(sections, sectionName, obj, store);
        }
    }

    private static readonly string[] CompactValueSectionSuffixes = { "Value", "Denotation" };

    /// <inheritdoc/>
    protected override EdsFileInfo ParseFileInfo(Dictionary<string, Dictionary<string, string>> sections)
    {
        var fileInfo = base.ParseFileInfo(sections);

        if (IniParser.HasSection(sections, "FileInfo"))
        {
            fileInfo.LastEds = IniParser.GetValue(sections, "FileInfo", "LastEDS");
        }

        return fileInfo;
    }

    /// <inheritdoc/>
    protected override bool IsKnownObjectEntryKey(string key) => SectionEntryKeys.IsDcfObjectKey(key);

    /// <inheritdoc/>
    protected override bool IsKnownSubObjectEntryKey(string key) => SectionEntryKeys.IsDcfSubObjectKey(key);

    /// <inheritdoc/>
    protected override CanOpenObject? ParseObject(Dictionary<string, Dictionary<string, string>> sections, ushort index)
    {
        var obj = base.ParseObject(sections, index);
        if (obj == null)
            return null;

        // DCF-specific fields
        var sectionName = ToHexInvariant(index);
        obj.ParameterValue = IniParser.GetValue(sections, sectionName, "ParameterValue");
        obj.Denotation = IniParser.GetValue(sections, sectionName, "Denotation");
        obj.ParamRefd = IniParser.GetValue(sections, sectionName, "ParamRefd");
        obj.UploadFile = IniParser.GetValue(sections, sectionName, "UploadFile");
        obj.DownloadFile = IniParser.GetValue(sections, sectionName, "DownloadFile");

        return obj;
    }

    /// <inheritdoc/>
    protected override void ParseSubObjects(Dictionary<string, Dictionary<string, string>> sections, ushort index, CanOpenObject obj)
    {
        base.ParseSubObjects(sections, index, obj);

        // Parse compact value storage (CiA 306 §5.2.3.2): keys are sub-indexes 1..254
        ApplyCompactListSection(
            sections,
            index,
            "Value",
            obj,
            static (subObj, value) => subObj.ParameterValue = value);

        // Parse compact denotation storage (CiA 306 §5.2.3.2)
        ApplyCompactListSection(
            sections,
            index,
            "Denotation",
            obj,
            static (subObj, denotation) => subObj.Denotation = denotation);
    }

    /// <inheritdoc/>
    protected override CanOpenSubObject? ParseSubObject(Dictionary<string, Dictionary<string, string>> sections, ushort index, byte subIndex)
    {
        var subObj = base.ParseSubObject(sections, index, subIndex);
        if (subObj == null)
            return null;

        // DCF-specific fields
        var sectionName = string.Concat(ToHexInvariant(index), "sub", ToHexInvariant(subIndex));
        subObj.ParameterValue = IniParser.GetValue(sections, sectionName, "ParameterValue");
        subObj.Denotation = IniParser.GetValue(sections, sectionName, "Denotation");
        subObj.ParamRefd = IniParser.GetValue(sections, sectionName, "ParamRefd");

        return subObj;
    }

    /// <inheritdoc/>
    protected override bool IsKnownSection(string sectionName)
    {
        if (base.IsKnownSection(sectionName))
            return true;

        // Check for compact value/denotation sections (hex index + "Value" or "Denotation").
        // The prefix must be pure hex digits: "[2000 Value]" is never probed by
        // ApplyCompactListSection and stays in AdditionalSections like "[1000sub 1]".
        // Note: ObjectLinks sections are intentionally NOT marked as known here.
        // This allows orphaned ObjectLinks (for non-existent objects) to be preserved in AdditionalSections.
        if (TryParseCompanionSuffixSection(sectionName, "Value", out _) ||
            TryParseCompanionSuffixSection(sectionName, "Denotation", out _))
            return true;

        return false;
    }

    #endregion

    #region DCF-only parsing methods

    private const string NormativeCommissioningSection = "DeviceComissioning";
    private const string CommonCommissioningSection = "DeviceCommissioning";

    private static DeviceCommissioning ParseDeviceCommissioning(
        DeviceConfigurationFile dcf,
        Dictionary<string, Dictionary<string, string>> sections)
    {
        var dc = new DeviceCommissioning();

        // CiA 306-1 v1.4.0 § 7.3.5 Table 12 spells the section "DeviceComissioning" (one 'm');
        // the two-'m' spelling is common in the wild. The normative spelling wins when both exist.
        var hasNormative = IniParser.HasSection(sections, NormativeCommissioningSection);
        var hasCommon = IniParser.HasSection(sections, CommonCommissioningSection);
        if (hasNormative && hasCommon)
            KeepSecondCommissioningSection(dcf, sections);

        var sectionName = hasNormative
            ? NormativeCommissioningSection
            : hasCommon
                ? CommonCommissioningSection
                : null;

        if (sectionName == null)
            return dc;

        dc.NodeId = LenientIniNumber.ParseByte(
            sections,
            sectionName,
            "NodeID",
            IniParser.GetValue(sections, sectionName, "NodeID", "1"),
            fallback: 1,
            code: ParseDiagnosticCodes.InvalidNodeId,
            coercedTo: "1",
            fallbackDescription: "Treated as 1.");
        if (!CanOpenNodeId.IsInRange(dc.NodeId))
        {
            // The value is kept: CanOpenModelValidator reports Node-ID 0 and values above 127.
            LenientIniNumber.ReportOutOfRange(
                sections,
                sectionName,
                "NodeID",
                IniParser.GetValue(sections, sectionName, "NodeID"),
                ParseDiagnosticCodes.InvalidNodeId,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Invalid NodeID '{0}' in [{1}]. CANopen Node-ID must be in range {2}.",
                    dc.NodeId,
                    sectionName,
                    CanOpenNodeId.RangeDescription));
        }

        dc.NodeName = IniParser.GetValue(sections, sectionName, "NodeName");
        dc.NodeRefd = IniParser.GetValue(sections, sectionName, "NodeRefd");
        dc.Baudrate = LenientIniNumber.ParseUInt16(
            sections,
            sectionName,
            "Baudrate",
            IniParser.GetValue(sections, sectionName, "Baudrate", "250"),
            fallback: 250,
            code: ParseDiagnosticCodes.InvalidBaudrate,
            coercedTo: "250",
            fallbackDescription: "Treated as 250.");
        dc.NetNumber = LenientIniNumber.ParseUInt32(
            sections,
            sectionName,
            "NetNumber",
            IniParser.GetValue(sections, sectionName, "NetNumber", "0"),
            fallback: 0,
            code: ParseDiagnosticCodes.InvalidNetNumber,
            coercedTo: "0",
            fallbackDescription: LenientIniNumber.TreatAsZero);
        dc.NetworkName = IniParser.GetValue(sections, sectionName, "NetworkName");
        dc.NetRefd = IniParser.GetValue(sections, sectionName, "NetRefd");
        dc.CANopenManager = IniKeyTokens.ParseBoolean(sections, sectionName, "CANopenManager");

        var lssSerialStr = IniParser.GetValue(sections, sectionName, "LSS_SerialNumber");
        if (!string.IsNullOrEmpty(lssSerialStr))
        {
            dc.LssSerialNumber = LenientIniNumber.ParseOptionalUInt32(
                sections,
                sectionName,
                "LSS_SerialNumber",
                lssSerialStr,
                ParseDiagnosticCodes.InvalidLssSerialNumber,
                LenientIniNumber.LeaveUnset);
        }

        CanOpenSectionParsers.CaptureUnmappedEntries(
            sections, sectionName, SectionEntryKeys.IsDeviceCommissioningKey, dc.RemainingEntries);

        return dc;
    }

    /// <summary>
    /// Both spellings are present: the second section is not read into the model. It is reported
    /// and kept unchanged in <see cref="DeviceConfigurationFile.AdditionalSections"/>
    /// (strict mode throws).
    /// </summary>
    private static void KeepSecondCommissioningSection(
        DeviceConfigurationFile dcf,
        Dictionary<string, Dictionary<string, string>> sections)
    {
        // The sections dictionary is case-insensitive; take the spelling the file used.
        var name = sections.Keys.First(k => string.Equals(k, CommonCommissioningSection, StringComparison.OrdinalIgnoreCase));
        var message = string.Format(
            CultureInfo.InvariantCulture,
            "Both [{0}] (CiA 306-1) and [{1}] are present. [{0}] is read; [{1}] is kept unchanged in AdditionalSections.",
            NormativeCommissioningSection,
            name);

        ParseDiagnosticScope.Report(new ParseDiagnostic(
            ParseSeverity.Warning,
            ParseDiagnosticCodes.IniDuplicateDeviceCommissioning,
            path: name,
            message: message,
            rawValue: name));

        if (StrictParsingScope.IsEnabled)
        {
            throw new EdsParseException(message)
            {
                Code = ParseDiagnosticCodes.IniDuplicateDeviceCommissioning,
                SectionName = name
            };
        }

        dcf.AdditionalSections[name] = AdditionalSectionsCloner.CloneSectionEntriesCaseInsensitive(sections[name]);
    }

    private static List<int> ParseConnectedModules(
        Dictionary<string, Dictionary<string, string>> sections,
        Dictionary<string, OrderedStringDictionary> store)
    {
        CanOpenSectionParsers.CaptureCountedListEntries(
            sections,
            "ConnectedModules",
            SectionEntryKeys.NrOfEntriesKey,
            store,
            static value => TryParseConnectedModule(value, out _));

        var modules = new List<int>();
        var count = CanOpenSectionParsers.ParseModuleCount(sections, "ConnectedModules");

        for (int i = 1; i <= count; i++)
        {
            var moduleStr = IniParser.GetValue(sections, "ConnectedModules", i.ToString(CultureInfo.InvariantCulture));
            if (TryParseConnectedModule(moduleStr, out var moduleNumber))
            {
                modules.Add(moduleNumber);
            }
        }

        return modules;
    }

    /// <summary>A <c>[ConnectedModules]</c> slot is loaded only when it holds a decimal module number.</summary>
    private static bool TryParseConnectedModule(string value, out int moduleNumber)
    {
        moduleNumber = 0;
        return !string.IsNullOrEmpty(value)
               && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out moduleNumber);
    }

    #endregion
}
