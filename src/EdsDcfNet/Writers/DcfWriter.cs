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
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public void WriteFile(DeviceConfigurationFile dcf, string filePath)
    {
        try
        {
            var content = GenerateDcfContent(dcf);
            File.WriteAllText(filePath, content, TextFileIo.Utf8NoBom);
        }
        catch (DcfWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DcfWriteException($"Failed to write DCF file to {filePath}", ex);
        }
    }

    /// <summary>
    /// Writes a DCF to the specified file path asynchronously.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to write</param>
    /// <param name="filePath">Path where the DCF file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public async Task WriteFileAsync(
        DeviceConfigurationFile dcf,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = GenerateDcfContent(dcf);
            await TextFileIo.WriteAllTextAsync(filePath, content, TextFileIo.Utf8NoBom, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DcfWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DcfWriteException($"Failed to write DCF file to {filePath}", ex);
        }
    }

    /// <summary>
    /// Generates DCF content as a string.
    /// </summary>
    /// <param name="dcf">The DeviceConfigurationFile to convert</param>
    /// <returns>DCF content as string</returns>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public string GenerateString(DeviceConfigurationFile dcf)
    {
        return GenerateDcfContent(dcf);
    }

    #region DCF-specific section overrides

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

    #endregion

    #region DCF-only sections

    private static void WriteDeviceCommissioning(StringBuilder sb, DeviceCommissioning dc)
    {
        sb.AppendLine("[DeviceCommissioning]");
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

        sb.AppendLine();
    }

    private static void WriteConnectedModules(StringBuilder sb, List<int> connectedModules)
    {
        sb.AppendLine("[ConnectedModules]");
        WriteKeyValue(sb, "NrOfEntries", connectedModules.Count.ToString(CultureInfo.InvariantCulture));

        for (int i = 0; i < connectedModules.Count; i++)
        {
            WriteKeyValue(sb, (i + 1).ToString(CultureInfo.InvariantCulture), connectedModules[i].ToString(CultureInfo.InvariantCulture));
        }

        sb.AppendLine();
    }

    private static void WriteDcfFileInfo(StringBuilder sb, EdsFileInfo fileInfo)
    {
        WriteFileInfo(sb, fileInfo);

        if (!string.IsNullOrEmpty(fileInfo.LastEds))
        {
            WriteKeyValue(sb, "LastEDS", fileInfo.LastEds);
        }

        sb.AppendLine();
    }

    #endregion

    private static string GenerateDcfContent(DeviceConfigurationFile dcf)
    {
        var sb = new StringBuilder();

        WriteSection("FileInfo", () => WriteDcfFileInfo(sb, dcf.FileInfo));

        WriteSection("DeviceInfo", () => WriteDeviceInfo(sb, dcf.DeviceInfo));

        WriteSection("DeviceCommissioning", () => WriteDeviceCommissioning(sb, dcf.DeviceCommissioning));

        if (dcf.ObjectDictionary.DummyUsage.Count > 0)
        {
            WriteSection("DummyUsage", () => WriteDummyUsage(sb, dcf.ObjectDictionary));
        }

        WriteSection("ObjectLists", () => WriteObjectLists(sb, dcf.ObjectDictionary));

        WriteSection("Objects", () => WriteObjects(sb, dcf.ObjectDictionary));

        if (dcf.SupportedModules.Count > 0)
        {
            WriteSection("SupportedModules", () => WriteSupportedModules(sb, dcf.SupportedModules));
        }

        if (dcf.ConnectedModules.Count > 0)
        {
            WriteSection("ConnectedModules", () => WriteConnectedModules(sb, dcf.ConnectedModules));
        }

        if (dcf.DynamicChannels != null && dcf.DynamicChannels.Segments.Count > 0)
        {
            WriteSection("DynamicChannels", () => WriteDynamicChannels(sb, dcf.DynamicChannels));
        }

        if (dcf.Tools.Count > 0)
        {
            WriteSection("Tools", () => WriteTools(sb, dcf.Tools));
        }

        if (dcf.Comments != null && dcf.Comments.CommentLines.Count > 0)
        {
            WriteSection("Comments", () => WriteComments(sb, dcf.Comments));
        }

        foreach (var section in dcf.AdditionalSections.OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (ObjectLinksSectionHelper.IsObjectLinksSectionForExistingObject(section.Key, dcf.ObjectDictionary))
            {
                continue;
            }

            WriteSection(section.Key, () => WriteAdditionalSection(sb, section.Key, section.Value));
        }

        return sb.ToString();
    }

    private static void WriteObjects(StringBuilder sb, ObjectDictionary objDict)
    {
        var allObjects = objDict.Objects.OrderBy(o => o.Key);

        foreach (var objEntry in allObjects)
        {
            var sectionName = string.Format(CultureInfo.InvariantCulture, "{0:X}", objEntry.Key);
            WriteSection(sectionName, () => Instance.WriteObject(sb, objEntry.Value, WriteSection));
        }
    }

    private static void WriteSection(string sectionName, Action writeAction)
    {
        try
        {
            writeAction();
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
}
