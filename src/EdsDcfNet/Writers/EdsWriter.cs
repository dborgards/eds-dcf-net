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
    /// <remarks>
    /// The content is written to a temporary file in the target directory and then moved or
    /// replaced over the target. On failure the target is left untouched and the temporary file
    /// is removed. Whether the final replace is atomic depends on the file system (for example,
    /// network shares may not guarantee it).
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="eds"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public void WriteFile(ElectronicDataSheet eds, string filePath)
    {
        ThrowIfNull(eds, nameof(eds));

        try
        {
            var content = GenerateEdsContent(eds);
            TextFileIo.WriteOutputTextToFile(filePath, content);
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
    /// Writes an EDS to the specified stream.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="stream">Writable destination stream. The stream is not disposed by this method.</param>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public void WriteStream(ElectronicDataSheet eds, Stream stream)
    {
        ThrowIfNull(eds, nameof(eds));
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));

        try
        {
            var content = GenerateEdsContent(eds);
            TextFileIo.WriteOutputText(stream, content);
        }
        catch (EdsWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new EdsWriteException("Failed to write EDS content to stream.", ex);
        }
    }

    /// <summary>
    /// Writes an EDS to the specified file path asynchronously.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="filePath">Path where the EDS file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <remarks>
    /// The content is written to a temporary file in the target directory and then moved or
    /// replaced over the target. On failure or cancellation the target is left untouched and the temporary file
    /// is removed. Whether the final replace is atomic depends on the file system (for example,
    /// network shares may not guarantee it).
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="eds"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public async Task WriteFileAsync(
        ElectronicDataSheet eds,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNull(eds, nameof(eds));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = GenerateEdsContent(eds);
            await TextFileIo.WriteOutputTextToFileAsync(filePath, content, cancellationToken).ConfigureAwait(false);
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
    /// Writes an EDS to the specified stream asynchronously.
    /// </summary>
    /// <param name="eds">The ElectronicDataSheet to write</param>
    /// <param name="stream">Writable destination stream. The stream is not disposed by this method.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public async Task WriteStreamAsync(
        ElectronicDataSheet eds,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNull(eds, nameof(eds));
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = GenerateEdsContent(eds);
            await TextFileIo.WriteOutputTextAsync(stream, content, cancellationToken).ConfigureAwait(false);
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
            throw new EdsWriteException("Failed to write EDS content to stream.", ex);
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
        ThrowIfNull(eds, nameof(eds));
        return GenerateEdsContent(eds);
    }

    private static string GenerateEdsContent(ElectronicDataSheet eds)
    {
        var sb = new StringBuilder();
        var sectionEntries = eds.SectionRemainingEntries;

        WriteSection("FileInfo", () =>
        {
            WriteFileInfo(sb, eds.FileInfo);
            WriteRemainingEntries(sb, eds.FileInfo.RemainingEntries, SectionEntryKeys.IsEdsFileInfoKey);
            sb.AppendLine();
        });

        WriteSection("DeviceInfo", () => WriteDeviceInfo(sb, eds.DeviceInfo));

        if (eds.ObjectDictionary.DummyUsage.Count > 0 || HasSectionEntries(sectionEntries, "DummyUsage"))
        {
            WriteSection("DummyUsage", () => WriteDummyUsage(sb, eds.ObjectDictionary, sectionEntries));
        }

        WriteSection("ObjectLists", () => WriteObjectLists(sb, eds.ObjectDictionary, sectionEntries));

        WriteSection("Objects", () => WriteObjects(sb, eds.ObjectDictionary, sectionEntries));

        if (eds.SupportedModules.Count > 0 || HasSectionEntries(sectionEntries, "SupportedModules"))
        {
            WriteSection("SupportedModules", () => WriteSupportedModules(sb, eds.SupportedModules, sectionEntries));
        }

        if (MustWriteDynamicChannels(eds.DynamicChannels))
        {
            WriteSection("DynamicChannels", () => WriteDynamicChannels(sb, eds.DynamicChannels!));
        }

        if (MustWriteTools(eds.Tools, sectionEntries))
        {
            WriteSection("Tools", () => WriteTools(sb, eds.Tools, sectionEntries));
        }

        if (MustWriteComments(eds.Comments))
        {
            WriteSection("Comments", () => WriteComments(sb, eds.Comments!));
        }

        foreach (var section in eds.AdditionalSectionOrder.Sections(eds.AdditionalSections))
        {
            if (ObjectLinksSectionHelper.IsObjectLinksSectionForExistingObject(section.Key, eds.ObjectDictionary))
            {
                continue;
            }

            WriteSection(
                section.Key,
                () => WriteAdditionalSection(
                    sb, section.Key, eds.AdditionalSectionOrder.Entries(section.Key, section.Value)));
        }

        return sb.ToString();
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
            throw new EdsWriteException(ex.Message)
            {
                SectionName = sectionName
            };
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

    private static void ThrowIfNull(object? value, string parameterName)
    {
        if (value == null)
            throw new ArgumentNullException(parameterName);
    }
}
