namespace EdsDcfNet.Writers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Writer for Device Configuration File (DCF) files.
/// </summary>
public class DcfWriter : IniWriterBase
{
    private static readonly DcfWriter Instance = new();

    /// <summary>
    /// Writes a DCF to the specified file path.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the DCF file should be written</param>
    /// <remarks>
    /// The content is written to a temporary file in the target directory and then moved or
    /// replaced over the target. On failure the target is left untouched and the temporary file
    /// is removed. Whether the final replace is atomic depends on the file system (for example,
    /// network shares may not guarantee it).
    /// A symbolic link is followed: its final target is replaced and the link is kept. The
    /// netstandard2.0 build cannot resolve links; it serializes the content completely and then
    /// overwrites the link target in place, which is not atomic.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="dcf"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public void WriteFile(DeviceConfigurationFile dcf, string filePath)
    {
        ThrowIfNull(dcf, nameof(dcf));

        WriteEntryPoints.ToFile(
            filePath,
            "DCF",
            () =>
            {
                var content = GenerateDcfContent(dcf);
                TextFileIo.WriteOutputTextToFile(filePath, content);
            },
            (message, inner) => new DcfWriteException(message, inner));
    }

    /// <summary>
    /// Writes a DCF to the specified stream.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="stream">Writable destination stream</param>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public void WriteStream(DeviceConfigurationFile dcf, Stream stream)
    {
        ThrowIfNull(dcf, nameof(dcf));
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));

        WriteEntryPoints.ToStream(
            "DCF",
            () =>
            {
                var content = GenerateDcfContent(dcf);
                TextFileIo.WriteOutputText(stream, content);
            },
            (message, inner) => new DcfWriteException(message, inner));
    }

    /// <summary>
    /// Writes a DCF to the specified file path asynchronously.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the DCF file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <remarks>
    /// The content is written to a temporary file in the target directory and then moved or
    /// replaced over the target. On failure or cancellation the target is left untouched and the temporary file
    /// is removed. Whether the final replace is atomic depends on the file system (for example,
    /// network shares may not guarantee it).
    /// A symbolic link is followed: its final target is replaced and the link is kept. The
    /// netstandard2.0 build cannot resolve links; it serializes the content completely and then
    /// overwrites the link target in place, which is not atomic.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="dcf"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public async Task WriteFileAsync(
        DeviceConfigurationFile dcf,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNull(dcf, nameof(dcf));

        await WriteEntryPoints.ToFileAsync(
            filePath,
            "DCF",
            async () =>
            {
                var content = GenerateDcfContent(dcf);
                await TextFileIo.WriteOutputTextToFileAsync(filePath, content, cancellationToken).ConfigureAwait(false);
            },
            (message, inner) => new DcfWriteException(message, inner),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a DCF to the specified stream asynchronously.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public async Task WriteStreamAsync(
        DeviceConfigurationFile dcf,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNull(dcf, nameof(dcf));
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));

        await WriteEntryPoints.ToStreamAsync(
            "DCF",
            async () =>
            {
                var content = GenerateDcfContent(dcf);
                await TextFileIo.WriteOutputTextAsync(stream, content, cancellationToken).ConfigureAwait(false);
            },
            (message, inner) => new DcfWriteException(message, inner),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Generates DCF content as a string.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to convert</param>
    /// <returns>DCF content as string</returns>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public string GenerateString(DeviceConfigurationFile dcf)
    {
        ThrowIfNull(dcf, nameof(dcf));
        return GenerateDcfContent(dcf);
    }

    #region DCF-specific section overrides

    /// <inheritdoc/>
    protected override bool IsDedicatedObjectEntryKey(string key) => SectionEntryKeys.IsDcfObjectKey(key);

    /// <inheritdoc/>
    protected override bool IsDedicatedSubObjectEntryKey(string key) => SectionEntryKeys.IsDcfSubObjectKey(key);

    /// <inheritdoc/>
    protected override void WriteObjectExtension(StringBuilder sb, CanOpenObject obj)
    {
        if (!string.IsNullOrEmpty(obj.ParameterValue))
        {
            WriteKeyValue(sb, "ParameterValue", obj.ParameterValue);
        }

        if (!string.IsNullOrEmpty(obj.Denotation))
        {
            WriteKeyValue(sb, "Denotation", obj.Denotation);
        }

        if (!string.IsNullOrEmpty(obj.ParamRefd))
        {
            WriteKeyValue(sb, "ParamRefd", obj.ParamRefd);
        }

        if (!string.IsNullOrEmpty(obj.UploadFile))
        {
            WriteKeyValue(sb, "UploadFile", obj.UploadFile);
        }

        if (!string.IsNullOrEmpty(obj.DownloadFile))
        {
            WriteKeyValue(sb, "DownloadFile", obj.DownloadFile);
        }
    }

    /// <inheritdoc/>
    protected override void WriteSubObjectExtension(StringBuilder sb, CanOpenSubObject subObj)
    {
        if (!string.IsNullOrEmpty(subObj.ParameterValue))
        {
            WriteKeyValue(sb, "ParameterValue", subObj.ParameterValue);
        }

        if (!string.IsNullOrEmpty(subObj.Denotation))
        {
            WriteKeyValue(sb, "Denotation", subObj.Denotation);
        }

        if (!string.IsNullOrEmpty(subObj.ParamRefd))
        {
            WriteKeyValue(sb, "ParamRefd", subObj.ParamRefd);
        }
    }

    /// <inheritdoc/>
    protected override void WriteCompactValueAndDenotationSections(
        StringBuilder sb,
        CanOpenObject obj,
        int compactMax,
        HashSet<byte> expandedSubIndexes,
        Action<string, Action> writeSection)
        => WriteCompactValueAndDenotationSections(
            sb,
            obj,
            compactMax,
            expandedSubIndexes,
            writeSection,
            CurrentObjectSectionEntries);

    /// <inheritdoc/>
    /// <remarks>
    /// Also called with <paramref name="compactMax"/> <c>0</c> for an object without compact
    /// storage: the reader applies <c>[xxxxValue]</c> / <c>[xxxxDenotation]</c> to every object,
    /// so a list section that only carries kept entries is written again.
    /// </remarks>
    private protected override void WriteCompactValueAndDenotationSections(
        StringBuilder sb,
        CanOpenObject obj,
        int compactMax,
        HashSet<byte> expandedSubIndexes,
        Action<string, Action> writeSection,
        Dictionary<string, OrderedStringDictionary>? sectionEntries)
    {
        WriteCompactListSection(
            sb,
            obj,
            compactMax,
            expandedSubIndexes,
            writeSection,
            "Value",
            SelectParameterValue,
            sectionEntries);

        WriteCompactListSection(
            sb,
            obj,
            compactMax,
            expandedSubIndexes,
            writeSection,
            "Denotation",
            SelectDenotation,
            sectionEntries);
    }

    /// <summary>Value of a sub-object in the DCF <c>[xxxxValue]</c> list.</summary>
    internal static string? SelectParameterValue(CanOpenSubObject subObj) => subObj.ParameterValue;

    /// <summary>Value of a sub-object in the DCF <c>[xxxxDenotation]</c> list.</summary>
    internal static string? SelectDenotation(CanOpenSubObject subObj) => subObj.Denotation;

    private static void WriteCompactListSection(
        StringBuilder sb,
        CanOpenObject obj,
        int compactMax,
        HashSet<byte> expandedSubIndexes,
        Action<string, Action> writeSection,
        string suffix,
        Func<CanOpenSubObject, string?> selectValue,
        Dictionary<string, OrderedStringDictionary>? sectionEntries)
    {
        var entries = GetCompactListEntries(obj, compactMax, expandedSubIndexes, (_, sub) => selectValue(sub));

        var sectionName = string.Format(CultureInfo.InvariantCulture, "{0:X}{1}", obj.Index, suffix);
        var kept = GetSectionEntries(sectionEntries, sectionName);
        if (entries.Count == 0 && kept == null)
            return;

        writeSection(
            sectionName,
            () =>
            {
                IniRoundTripText.WriteSectionHeader(
                    sb,
                    string.Format(CultureInfo.InvariantCulture, "{0:X}{1}", obj.Index, suffix));
                WriteKeyValue(sb, "NrOfEntries", entries.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var entry in entries)
                {
                    WriteKeyValue(sb, entry.Key.ToString(CultureInfo.InvariantCulture), entry.Value);
                }

                WriteCompactListRemainingEntries(sb, kept, entries.Keys);
                sb.AppendLine();
            });
    }

    #endregion

    #region DCF-only sections

    private static void WriteDeviceCommissioning(StringBuilder sb, DeviceCommissioning dc)
    {
        if (!CanOpenNodeId.IsInRange(dc.NodeId))
        {
            throw new DcfWriteException(
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot write DCF: NodeId {0} is outside the valid CANopen range " + CanOpenNodeId.RangeDescription + ".",
                    dc.NodeId),
                "DeviceCommissioning");
        }

        // CiA 306-1 Table 12 spells the section with a single "m"; the reader accepts both spellings.
        IniRoundTripText.WriteSectionHeader(sb, "DeviceComissioning");
        WriteKeyValue(sb, "NodeID", dc.NodeId.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "NodeName", dc.NodeName);

        if (!string.IsNullOrEmpty(dc.NodeRefd))
        {
            WriteKeyValue(sb, "NodeRefd", dc.NodeRefd);
        }

        WriteKeyValue(sb, "Baudrate", dc.Baudrate.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "NetNumber", dc.NetNumber.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue(sb, "NetworkName", dc.NetworkName);

        if (!string.IsNullOrEmpty(dc.NetRefd))
        {
            WriteKeyValue(sb, "NetRefd", dc.NetRefd);
        }

        WriteKeyValue(sb, "CANopenManager", ValueConverter.FormatBoolean(dc.CANopenManager));

        if (dc.LssSerialNumber.HasValue)
        {
            WriteKeyValue(sb, "LSS_SerialNumber", dc.LssSerialNumber.Value.ToString(CultureInfo.InvariantCulture));
        }

        WriteRemainingEntries(sb, dc.RemainingEntries, SectionEntryKeys.IsDeviceCommissioningKey);
        sb.AppendLine();
    }

    private static void WriteConnectedModules(
        StringBuilder sb,
        List<int> connectedModules,
        Dictionary<string, OrderedStringDictionary> sectionEntries)
    {
        IniRoundTripText.WriteSectionHeader(sb, "ConnectedModules");
        WriteKeyValue(sb, "NrOfEntries", connectedModules.Count.ToString(CultureInfo.InvariantCulture));

        for (int i = 0; i < connectedModules.Count; i++)
        {
            WriteKeyValue(sb, (i + 1).ToString(CultureInfo.InvariantCulture), connectedModules[i].ToString(CultureInfo.InvariantCulture));
        }

        WriteCountedListRemainingEntries(
            sb,
            GetSectionEntries(sectionEntries, "ConnectedModules"),
            SectionEntryKeys.NrOfEntriesKey,
            connectedModules.Count);
        sb.AppendLine();
    }

    private static void WriteDcfFileInfo(StringBuilder sb, EdsFileInfo fileInfo)
    {
        WriteFileInfo(sb, fileInfo);

        if (!string.IsNullOrEmpty(fileInfo.LastEds))
        {
            WriteKeyValue(sb, "LastEDS", fileInfo.LastEds);
        }

        WriteRemainingEntries(sb, fileInfo.RemainingEntries, key => SectionEntryKeys.IsWrittenDcfFileInfoKey(key, fileInfo.LastEds));
        sb.AppendLine();
    }

    #endregion

    private static string GenerateDcfContent(DeviceConfigurationFile dcf)
    {
        var sb = new StringBuilder();
        WriteGeneratedSections(sb, dcf);

        // Collected before the first additional section is written, and only when there is one.
        HashSet<string>? generatedHeaders = null;
        foreach (var section in dcf.AdditionalSectionOrder.Sections(dcf.AdditionalSections))
        {
            generatedHeaders ??= GetGeneratedSectionHeaders(sb);
            if (ObjectLinksSectionHelper.IsObjectLinksSectionForExistingObject(section.Key, dcf.ObjectDictionary) ||
                IsGeneratedSection(generatedHeaders, section.Key))
            {
                continue;
            }

            // The reader keeps the second of two commissioning spellings here. A stale copy of the
            // generated name would duplicate the section: the generated one wins.
            if (DeviceCommissioningSemantics.IsDiscardedAdditionalSection(section.Key, dcf.DeviceCommissioning))
            {
                continue;
            }

            WriteSection(
                section.Key,
                () => WriteAdditionalSection(
                    sb, section.Key, dcf.AdditionalSectionOrder.Entries(section.Key, section.Value)));
        }

        return TextFileIo.ApplyOutputNewLine(sb.ToString());
    }

    /// <summary>
    /// The section headers this writer generates from the model, before the additional
    /// sections. Validated writes use it to check only the additional sections that are
    /// written (<see cref="IniWriterBase.GetGeneratedSectionHeaders(StringBuilder)"/>).
    /// </summary>
    internal static HashSet<string> CollectGeneratedSectionHeaders(DeviceConfigurationFile dcf)
    {
        var sb = new StringBuilder();
        WriteGeneratedSections(sb, dcf);
        return GetGeneratedSectionHeaders(sb);
    }

    /// <summary>Writes every section generated from the model; additional sections follow.</summary>
    private static void WriteGeneratedSections(StringBuilder sb, DeviceConfigurationFile dcf)
    {
        var sectionEntries = dcf.SectionRemainingEntries;

        WriteSection("FileInfo", () => WriteDcfFileInfo(sb, dcf.FileInfo));

        WriteSection("DeviceInfo", () => WriteDeviceInfo(sb, dcf.DeviceInfo));

        if (DeviceCommissioningSemantics.IsWrittenToDcf(dcf.DeviceCommissioning))
        {
            WriteSection("DeviceCommissioning", () => WriteDeviceCommissioning(sb, dcf.DeviceCommissioning));
        }

        if (dcf.ObjectDictionary.DummyUsage.Count > 0 || HasSectionEntries(sectionEntries, "DummyUsage"))
        {
            WriteSection("DummyUsage", () => WriteDummyUsage(sb, dcf.ObjectDictionary, sectionEntries));
        }

        WriteSection("ObjectLists", () => WriteObjectLists(sb, dcf.ObjectDictionary, sectionEntries));

        WriteSection("Objects", () => WriteObjects(sb, dcf.ObjectDictionary, sectionEntries));

        if (dcf.SupportedModules.Count > 0 || HasSectionEntries(sectionEntries, "SupportedModules"))
        {
            WriteSection("SupportedModules", () => WriteSupportedModules(sb, dcf.SupportedModules, sectionEntries));
        }

        if (dcf.ConnectedModules.Count > 0 || HasSectionEntries(sectionEntries, "ConnectedModules"))
        {
            WriteSection("ConnectedModules", () => WriteConnectedModules(sb, dcf.ConnectedModules, sectionEntries));
        }

        if (MustWriteDynamicChannels(dcf.DynamicChannels))
        {
            WriteSection("DynamicChannels", () => WriteDynamicChannels(sb, dcf.DynamicChannels!));
        }

        if (MustWriteTools(dcf.Tools, sectionEntries))
        {
            WriteSection("Tools", () => WriteTools(sb, dcf.Tools, sectionEntries));
        }

        if (MustWriteComments(dcf.Comments))
        {
            WriteSection("Comments", () => WriteComments(sb, dcf.Comments!));
        }
    }

    private static void WriteObjects(
        StringBuilder sb,
        ObjectDictionary objDict,
        Dictionary<string, OrderedStringDictionary> sectionEntries)
    {
        var allObjects = objDict.Objects.OrderBy(o => o.Key);

        foreach (var objEntry in allObjects)
        {
            var sectionName = string.Format(CultureInfo.InvariantCulture, "{0:X}", objEntry.Key);
            WriteSection(sectionName, () => Instance.WriteObject(sb, objEntry.Value, WriteSection, sectionEntries));
        }
    }

    private static void WriteSection(string sectionName, Action writeAction)
    {
        try
        {
            writeAction();
        }
        catch (IniTextRejectedException ex)
        {
            throw new DcfWriteException(ex.Message)
            {
                SectionName = sectionName
            };
        }
        catch (DcfWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DcfWriteException(
                $"Failed to write section [{sectionName}]",
                ex)
            {
                SectionName = sectionName
            };
        }
    }

    private static void ThrowIfNull(object? value, string parameterName)
    {
        if (value == null)
            throw new ArgumentNullException(parameterName);
    }
}
