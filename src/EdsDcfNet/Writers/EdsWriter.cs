namespace EdsDcfNet.Writers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Writer for Electronic Data Sheet (EDS) files.
/// </summary>
public class EdsWriter : IniWriterBase
{
    private static readonly EdsWriter Instance = new();

    /// <summary>
    /// Writes an EDS to the specified file path.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the EDS file should be written</param>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public void WriteFile(ElectronicDataSheet eds, string filePath)
    {
        try
        {
            var content = GenerateEdsContent(eds);
            File.WriteAllText(filePath, content, TextFileIo.Utf8NoBom);
        }
        catch (EdsWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new EdsWriteException($"Failed to write EDS file to {filePath}", ex);
        }
    }

    /// <summary>
    /// Writes an EDS to the specified file path asynchronously.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the EDS file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public async Task WriteFileAsync(
        ElectronicDataSheet eds,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = GenerateEdsContent(eds);
            await TextFileIo.WriteAllTextAsync(filePath, content, TextFileIo.Utf8NoBom, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EdsWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new EdsWriteException($"Failed to write EDS file to {filePath}", ex);
        }
    }

    /// <summary>
    /// Generates EDS content as a string.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to convert</param>
    /// <returns>EDS content as string</returns>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public string GenerateString(ElectronicDataSheet eds)
    {
        return GenerateEdsContent(eds);
    }

    private static string GenerateEdsContent(ElectronicDataSheet eds)
    {
        var sb = new StringBuilder();

        WriteSection("FileInfo", () =>
        {
            WriteFileInfo(sb, eds.FileInfo);
            sb.AppendLine();
        });

        WriteSection("DeviceInfo", () => WriteDeviceInfo(sb, eds.DeviceInfo));

        if (eds.ObjectDictionary.DummyUsage.Count > 0)
        {
            WriteSection("DummyUsage", () => WriteDummyUsage(sb, eds.ObjectDictionary));
        }

        WriteSection("ObjectLists", () => WriteObjectLists(sb, eds.ObjectDictionary));

        WriteSection("Objects", () => WriteObjects(sb, eds.ObjectDictionary));

        if (eds.SupportedModules.Count > 0)
        {
            WriteSection("SupportedModules", () => WriteSupportedModules(sb, eds.SupportedModules));
        }

        if (eds.DynamicChannels != null && eds.DynamicChannels.Segments.Count > 0)
        {
            WriteSection("DynamicChannels", () => WriteDynamicChannels(sb, eds.DynamicChannels));
        }

        if (eds.Tools.Count > 0)
        {
            WriteSection("Tools", () => WriteTools(sb, eds.Tools));
        }

        if (eds.Comments != null && eds.Comments.CommentLines.Count > 0)
        {
            WriteSection("Comments", () => WriteComments(sb, eds.Comments));
        }

        foreach (var section in eds.AdditionalSections.OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (ObjectLinksSectionHelper.IsObjectLinksSectionForExistingObject(section.Key, eds.ObjectDictionary))
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
        catch (EdsWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new EdsWriteException(
                $"Failed to write section [{sectionName}]",
                ex)
            {
                SectionName = sectionName
            };
        }
    }
}
