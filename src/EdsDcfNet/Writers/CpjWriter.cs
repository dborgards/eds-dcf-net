namespace EdsDcfNet.Writers;

using System.Globalization;
using System.Text;
using EdsDcfNet.Models;

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
    public static void WriteFile(NodelistProject cpj, string filePath)
    {
        var content = GenerateCpjContent(cpj);
        File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>
    /// Generates CPJ content as a string.
    /// </summary>
    /// <param name="cpj">The NodelistProject to convert</param>
    /// <returns>CPJ content as string</returns>
    public static string GenerateString(NodelistProject cpj)
    {
        return GenerateCpjContent(cpj);
    }

    private static string GenerateCpjContent(NodelistProject cpj)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < cpj.Networks.Count; i++)
        {
            var sectionName = i == 0 ? "Topology" : string.Format(CultureInfo.InvariantCulture, "Topology{0}", i + 1);
            WriteTopology(sb, cpj.Networks[i], sectionName);
        }

        foreach (var section in cpj.AdditionalSections)
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[{0}]", section.Key));
            foreach (var entry in section.Value)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}={1}", entry.Key, entry.Value));
            }
            sb.AppendLine();
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
}
