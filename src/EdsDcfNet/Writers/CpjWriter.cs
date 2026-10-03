namespace EdsDcfNet.Writers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Writer for CiA 306-3 nodelist project (.cpj) files.
/// </summary>
public class CpjWriter
{
    /// <summary>
    /// Writes a CPJ to the specified file path.
    /// </summary>
    /// <param name="cpj">The NodelistProject to write</param>
    /// <param name="filePath">Path where the CPJ file should be written</param>
    /// <remarks>
    /// The content is written to a temporary file in the target directory and then moved or
    /// replaced over the target. On failure the target is left untouched and the temporary file
    /// is removed. Whether the final replace is atomic depends on the file system (for example,
    /// network shares may not guarantee it).
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="cpj"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public void WriteFile(NodelistProject cpj, string filePath)
    {
        ThrowIfNull(cpj, nameof(cpj));

        try
        {
            var content = GenerateCpjContent(cpj);
            TextFileIo.WriteOutputTextToFile(filePath, content);
        }
        catch (CpjWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CpjWriteException($"Failed to write CPJ file to {filePath}", ex);
        }
    }

    /// <summary>
    /// Writes a CPJ to the specified stream.
    /// </summary>
    /// <param name="cpj">The NodelistProject to write</param>
    /// <param name="stream">Writable destination stream</param>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public void WriteStream(NodelistProject cpj, Stream stream)
    {
        ThrowIfNull(cpj, nameof(cpj));
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));

        try
        {
            var content = GenerateCpjContent(cpj);
            TextFileIo.WriteOutputText(stream, content);
        }
        catch (CpjWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CpjWriteException("Failed to write CPJ content to stream.", ex);
        }
    }

    /// <summary>
    /// Writes a CPJ to the specified file path asynchronously.
    /// </summary>
    /// <param name="cpj">The NodelistProject to write</param>
    /// <param name="filePath">Path where the CPJ file should be written</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <remarks>
    /// The content is written to a temporary file in the target directory and then moved or
    /// replaced over the target. On failure or cancellation the target is left untouched and the temporary file
    /// is removed. Whether the final replace is atomic depends on the file system (for example,
    /// network shares may not guarantee it).
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="cpj"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public async Task WriteFileAsync(
        NodelistProject cpj,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNull(cpj, nameof(cpj));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = GenerateCpjContent(cpj);
            await TextFileIo.WriteOutputTextToFileAsync(filePath, content, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CpjWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CpjWriteException($"Failed to write CPJ file to {filePath}", ex);
        }
    }

    /// <summary>
    /// Writes a CPJ to the specified stream asynchronously.
    /// </summary>
    /// <param name="cpj">The NodelistProject to write</param>
    /// <param name="stream">Writable destination stream</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public async Task WriteStreamAsync(
        NodelistProject cpj,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNull(cpj, nameof(cpj));
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = GenerateCpjContent(cpj);
            await TextFileIo.WriteOutputTextAsync(stream, content, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CpjWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CpjWriteException("Failed to write CPJ content to stream.", ex);
        }
    }

    /// <summary>
    /// Generates CPJ content as a string.
    /// </summary>
    /// <param name="cpj">The NodelistProject to convert</param>
    /// <returns>CPJ content as string</returns>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public string GenerateString(NodelistProject cpj)
    {
        ThrowIfNull(cpj, nameof(cpj));
        return GenerateCpjContent(cpj);
    }

    private static string GenerateCpjContent(NodelistProject cpj)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < cpj.Networks.Count; i++)
        {
            var sectionName = i == 0 ? "Topology" : string.Format(CultureInfo.InvariantCulture, "Topology{0}", i + 1);
            WriteSection(sectionName, () => WriteTopology(sb, cpj.Networks[i], sectionName));
        }

        foreach (var section in cpj.AdditionalSectionOrder.Sections(cpj.AdditionalSections))
        {
            WriteSection(
                section.Key,
                () =>
                {
                    IniRoundTripText.WriteSectionHeader(sb, section.Key);
                    foreach (var entry in cpj.AdditionalSectionOrder.Entries(section.Key, section.Value))
                    {
                        IniRoundTripText.WriteKeyValue(sb, entry.Key, entry.Value);
                    }
                    sb.AppendLine();
                });
        }

        return TextFileIo.ApplyOutputNewLine(sb.ToString());
    }

    private static void WriteTopology(StringBuilder sb, NetworkTopology topology, string sectionName)
    {
        IniRoundTripText.WriteSectionHeader(sb, sectionName);

        if (!string.IsNullOrEmpty(topology.NetName))
        {
            IniRoundTripText.WriteKeyValue(sb, "NetName", topology.NetName);
        }

        if (!string.IsNullOrEmpty(topology.NetRefd))
        {
            IniRoundTripText.WriteKeyValue(sb, "NetRefd", topology.NetRefd);
        }

        // Write Nodes count as hex
        var nodeCount = topology.Nodes.Count;
        IniRoundTripText.WriteKeyValue(
            sb,
            "Nodes",
            string.Format(CultureInfo.InvariantCulture, "0x{0:X2}", nodeCount));

        // Write nodes ordered by node ID
        foreach (var nodeEntry in topology.Nodes.OrderBy(n => n.Key))
        {
            var node = nodeEntry.Value;
            var prefix = string.Format(CultureInfo.InvariantCulture, "Node{0}", node.NodeId);

            IniRoundTripText.WriteKeyValue(
                sb,
                string.Format(CultureInfo.InvariantCulture, "{0}Present", prefix),
                node.Present ? "0x01" : "0x00");

            if (!string.IsNullOrEmpty(node.Name))
            {
                IniRoundTripText.WriteKeyValue(
                    sb,
                    string.Format(CultureInfo.InvariantCulture, "{0}Name", prefix),
                    node.Name);
            }

            if (!string.IsNullOrEmpty(node.Refd))
            {
                IniRoundTripText.WriteKeyValue(
                    sb,
                    string.Format(CultureInfo.InvariantCulture, "{0}Refd", prefix),
                    node.Refd);
            }

            if (!string.IsNullOrEmpty(node.DcfFileName))
            {
                IniRoundTripText.WriteKeyValue(
                    sb,
                    string.Format(CultureInfo.InvariantCulture, "{0}DCFName", prefix),
                    node.DcfFileName);
            }
        }

        if (!string.IsNullOrEmpty(topology.EdsBaseName))
        {
            IniRoundTripText.WriteKeyValue(sb, "EDSBaseName", topology.EdsBaseName);
        }

        sb.AppendLine();
    }

    private static void WriteSection(string sectionName, Action writeAction)
    {
        try
        {
            writeAction();
        }
        catch (IniTextRejectedException ex)
        {
            throw new CpjWriteException(ex.Message)
            {
                SectionName = sectionName
            };
        }
        catch (CpjWriteException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CpjWriteException(
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
