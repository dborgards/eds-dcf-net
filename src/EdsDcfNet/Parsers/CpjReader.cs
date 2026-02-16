namespace EdsDcfNet.Parsers;

using System.Globalization;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Reader for CiA 306-3 nodelist project (.cpj) files.
/// </summary>
public class CpjReader
{
    private readonly IniParser _iniParser = new();

    /// <summary>
    /// Reads a CPJ file from the specified path.
    /// </summary>
    /// <param name="filePath">Path to the CPJ file</param>
    /// <returns>Parsed NodelistProject object</returns>
    public NodelistProject ReadFile(string filePath)
    {
        var sections = _iniParser.ParseFile(filePath);
        return ParseCpj(sections);
    }

    /// <summary>
    /// Reads a CPJ from a string.
    /// </summary>
    /// <param name="content">CPJ file content as string</param>
    /// <returns>Parsed NodelistProject object</returns>
    public NodelistProject ReadString(string content)
    {
        var sections = _iniParser.ParseString(content);
        return ParseCpj(sections);
    }

    private NodelistProject ParseCpj(Dictionary<string, Dictionary<string, string>> sections)
    {
        var project = new NodelistProject();

        foreach (var sectionName in sections.Keys)
        {
            if (IsTopologySection(sectionName))
            {
                var topology = ParseTopology(sections, sectionName);
                project.Networks.Add(topology);
            }
            else
            {
                project.AdditionalSections[sectionName] = new Dictionary<string, string>(sections[sectionName]);
            }
        }

        return project;
    }

    private static bool IsTopologySection(string sectionName)
    {
        return sectionName.Equals("Topology", StringComparison.OrdinalIgnoreCase) ||
               (sectionName.StartsWith("Topology", StringComparison.OrdinalIgnoreCase) &&
                sectionName.Length > 8 &&
                int.TryParse(sectionName.Substring(8), NumberStyles.Integer, CultureInfo.InvariantCulture, out _));
    }

    private static NetworkTopology ParseTopology(Dictionary<string, Dictionary<string, string>> sections, string sectionName)
    {
        var topology = new NetworkTopology
        {
            NetName = IniParser.GetValue(sections, sectionName, "NetName"),
            NetRefd = IniParser.GetValue(sections, sectionName, "NetRefd"),
            EdsBaseName = IniParser.GetValue(sections, sectionName, "EDSBaseName")
        };

        // Normalize empty strings to null for optional fields
        if (string.IsNullOrEmpty(topology.NetName)) topology.NetName = null;
        if (string.IsNullOrEmpty(topology.NetRefd)) topology.NetRefd = null;
        if (string.IsNullOrEmpty(topology.EdsBaseName)) topology.EdsBaseName = null;

        // Parse nodes: scan Node IDs 1-127
        for (int nodeId = 1; nodeId <= 127; nodeId++)
        {
            var prefix = string.Format(CultureInfo.InvariantCulture, "Node{0}", nodeId);
            var presentValue = IniParser.GetValue(sections, sectionName, prefix + "Present");

            if (string.IsNullOrEmpty(presentValue))
                continue;

            var present = presentValue.Equals("0x01", StringComparison.OrdinalIgnoreCase) ||
                          presentValue == "1";

            var node = new NetworkNode
            {
                NodeId = (byte)nodeId,
                Present = present,
                Name = NullIfEmpty(IniParser.GetValue(sections, sectionName, prefix + "Name")),
                Refd = NullIfEmpty(IniParser.GetValue(sections, sectionName, prefix + "Refd")),
                DcfFileName = NullIfEmpty(IniParser.GetValue(sections, sectionName, prefix + "DCFName"))
            };

            topology.Nodes[(byte)nodeId] = node;
        }

        return topology;
    }

    private static string? NullIfEmpty(string value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
