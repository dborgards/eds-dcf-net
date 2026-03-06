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
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public void WriteFile(NodelistProject cpj, string filePath)
    {
        try
        {
            var content = GenerateCpjContent(cpj);
            File.WriteAllText(filePath, content, TextFileIo.Utf8NoBom);
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
    [ExcludeFromCodeCoverage]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public void WriteStream(NodelistProject cpj, Stream stream)
    {
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));

        try
        {
            var content = GenerateCpjContent(cpj);
            TextFileIo.WriteAllText(stream, content, TextFileIo.Utf8NoBom, leaveOpen: true);
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
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public async Task WriteFileAsync(
        NodelistProject cpj,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = GenerateCpjContent(cpj);
            await TextFileIo.WriteAllTextAsync(filePath, content, TextFileIo.Utf8NoBom, cancellationToken).ConfigureAwait(false);
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
    [ExcludeFromCodeCoverage]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public async Task WriteStreamAsync(
        NodelistProject cpj,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNull(stream, nameof(stream));
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = GenerateCpjContent(cpj);
            await TextFileIo.WriteAllTextAsync(stream, content, TextFileIo.Utf8NoBom, leaveOpen: true, cancellationToken: cancellationToken).ConfigureAwait(false);
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

        foreach (var section in cpj.AdditionalSections.OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
        {
            WriteSection(
                section.Key,
                () =>
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[{0}]", section.Key));
                    foreach (var entry in section.Value.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}={1}", entry.Key, entry.Value));
                    }
                    sb.AppendLine();
                });
        }

        return sb.ToString();
    }

    private static void WriteTopology(StringBuilder sb, NetworkTopology topology, string sectionName)
    {
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[{0}]", sectionName));

        if (!string.IsNullOrEmpty(topology.NetName))
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "NetName={0}", topology.NetName));
        }

        if (!string.IsNullOrEmpty(topology.NetRefd))
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "NetRefd={0}", topology.NetRefd));
        }

        // Write Nodes count as hex
        var nodeCount = topology.Nodes.Count;
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Nodes=0x{0:X2}", nodeCount));

        // Write nodes ordered by node ID
        foreach (var nodeEntry in topology.Nodes.OrderBy(n => n.Key))
        {
            var node = nodeEntry.Value;
            var prefix = string.Format(CultureInfo.InvariantCulture, "Node{0}", node.NodeId);

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}Present={1}", prefix, node.Present ? "0x01" : "0x00"));

            if (!string.IsNullOrEmpty(node.Name))
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}Name={1}", prefix, node.Name));
            }

            if (!string.IsNullOrEmpty(node.Refd))
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}Refd={1}", prefix, node.Refd));
            }

            if (!string.IsNullOrEmpty(node.DcfFileName))
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}DCFName={1}", prefix, node.DcfFileName));
            }
        }

        if (!string.IsNullOrEmpty(topology.EdsBaseName))
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "EDSBaseName={0}", topology.EdsBaseName));
        }

        sb.AppendLine();
    }

    private static void WriteSection(string sectionName, Action writeAction)
    {
        try
        {
            writeAction();
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
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value, parameterName);
#else
        if (value == null)
            throw new ArgumentNullException(parameterName);
#endif
    }
}
