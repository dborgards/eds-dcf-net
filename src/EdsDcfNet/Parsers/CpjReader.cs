namespace EdsDcfNet.Parsers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

#pragma warning disable CA1846 // span-based overloads not available in netstandard2.0

/// <summary>
/// Reader for CiA 306-3 nodelist project (.cpj) files.
/// </summary>
public class CpjReader : IFileReader<NodelistProject>
{
    /// <summary>
    /// Reads a CPJ file from the specified path.
    /// </summary>
    /// <param name="filePath">Path to the CPJ file</param>
    /// <param name="maxInputSize">Maximum file size in bytes.</param>
    /// <returns>Parsed NodelistProject object</returns>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public NodelistProject ReadFile(
        string filePath,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
    {
        var sections = IniParser.ParseFile(filePath, maxInputSize);
        return ParseCpj(sections);
    }

    /// <summary>
    /// Reads a CPJ file from a stream.
    /// </summary>
    /// <param name="stream">Readable stream containing CPJ content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters.</param>
    /// <returns>Parsed NodelistProject object</returns>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public NodelistProject ReadStream(
        Stream stream,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
    {
        var sections = IniParser.ParseStream(stream, maxInputSize);
        return ParseCpj(sections);
    }

    /// <summary>
    /// Reads a CPJ file from the specified path asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the CPJ file</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed NodelistProject object</returns>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public Task<NodelistProject> ReadFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => ReadFileAsync(filePath, ReaderDefaults.DefaultMaxInputSize, cancellationToken);

    /// <summary>
    /// Reads a CPJ file from the specified path asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the CPJ file</param>
    /// <param name="maxInputSize">Maximum file size in bytes.</param>
    /// <param name="cancellationToken">Cancellation token for aborting file I/O</param>
    /// <returns>Parsed NodelistProject object</returns>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public async Task<NodelistProject> ReadFileAsync(
        string filePath,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var sections = await IniParser.ParseFileAsync(filePath, maxInputSize, cancellationToken).ConfigureAwait(false);
        return ParseCpj(sections);
    }

    /// <summary>
    /// Reads a CPJ file from a stream asynchronously.
    /// </summary>
    /// <param name="stream">Readable stream containing CPJ content.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed NodelistProject object</returns>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public Task<NodelistProject> ReadStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => ReadStreamAsync(stream, ReaderDefaults.DefaultMaxInputSize, cancellationToken);

    /// <summary>
    /// Reads a CPJ file from a stream asynchronously.
    /// </summary>
    /// <param name="stream">Readable stream containing CPJ content.</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters.</param>
    /// <param name="cancellationToken">Cancellation token for aborting stream I/O</param>
    /// <returns>Parsed NodelistProject object</returns>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public async Task<NodelistProject> ReadStreamAsync(
        Stream stream,
        long maxInputSize,
        CancellationToken cancellationToken = default)
    {
        var sections = await IniParser.ParseStreamAsync(stream, maxInputSize, cancellationToken).ConfigureAwait(false);
        return ParseCpj(sections);
    }

    /// <summary>
    /// Reads a CPJ from a string.
    /// </summary>
    /// <param name="content">CPJ file content as string</param>
    /// <param name="maxInputSize">Maximum decoded content length in characters.</param>
    /// <returns>Parsed NodelistProject object</returns>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Public API — changing to static would be a breaking change for callers using instance syntax.")]
    public NodelistProject ReadString(
        string content,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
    {
        var sections = IniParser.ParseString(content, maxInputSize);
        return ParseCpj(sections);
    }

    private static NodelistProject ParseCpj(Dictionary<string, Dictionary<string, string>> sections)
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
                project.AdditionalSections[sectionName] =
                    new Dictionary<string, string>(sections[sectionName], StringComparer.OrdinalIgnoreCase);
            }
        }

        project.AdditionalSectionOrder.Capture(project.AdditionalSections, sections);
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

        var declaredNodes = ParseDeclaredNodeCount(sections, sectionName);

        // Parse nodes: scan Node IDs 1-127
        for (int nodeId = CanOpenNodeId.MinValue; nodeId <= CanOpenNodeId.MaxValue; nodeId++)
        {
            var prefix = string.Format(CultureInfo.InvariantCulture, "Node{0}", nodeId);
            var presentKey = prefix + "Present";

            if (!sections[sectionName].TryGetValue(presentKey, out var presentValue))
                continue;

            var present = ParsePresent(sections, sectionName, presentKey, presentValue);
            if (present == null)
                continue;

            var node = new NetworkNode
            {
                NodeId = (byte)nodeId,
                Present = present.Value,
                Name = NullIfEmpty(IniParser.GetValue(sections, sectionName, prefix + "Name")),
                Refd = NullIfEmpty(IniParser.GetValue(sections, sectionName, prefix + "Refd")),
                DcfFileName = NullIfEmpty(IniParser.GetValue(sections, sectionName, prefix + "DCFName"))
            };

            topology.Nodes[(byte)nodeId] = node;
        }

        // Everything the loop above did not map onto a property stays verbatim: vendor keys and the
        // entries of a node without a loaded NodeXPresent (CiA 306-3 Table 3).
        CanOpenSectionParsers.CaptureUnmappedEntries(
            sections,
            sectionName,
            key => SectionEntryKeys.IsWrittenTopologyKey(topology, key),
            topology.RemainingEntries);
        ReportNodeEntriesWithoutPresent(sections, sectionName, topology);

        if (declaredNodes != null && declaredNodes.Value != topology.Nodes.Count)
        {
            Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
                Diagnostics.ParseSeverity.Warning,
                Diagnostics.ParseDiagnosticCodes.CpjNodeCountMismatch,
                path: sectionName + ".Nodes",
                line: IniKeyLines.TryGetLine(sections, sectionName, "Nodes"),
                rawValue: IniParser.GetValue(sections, sectionName, "Nodes"),
                message: string.Format(
                    CultureInfo.InvariantCulture,
                    "Nodes declares {0} node(s) but the section has {1} NodeXPresent entries. The writer emits the number of nodes in the model.",
                    declaredNodes.Value,
                    topology.Nodes.Count)));
        }

        return topology;
    }

    /// <summary>
    /// CiA 306-3 Table 3: <c>NodeXPresent</c> is mandatory for each existing node and a missing entry
    /// means "not present". A <c>NodeXName</c>, <c>NodeXRefd</c> or <c>NodeXDCFName</c> without it is
    /// valid but incomplete, so it is only reported (strict mode does not throw) and the entry is kept.
    /// A <c>NodeXPresent</c> that exists with an empty value is already reported as reserved.
    /// </summary>
    private static void ReportNodeEntriesWithoutPresent(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        NetworkTopology topology)
    {
        foreach (var entry in topology.RemainingEntries)
        {
            if (!SectionEntryKeys.TryParseTopologyNodeKey(entry.Key, out var nodeId, out var suffix)
                || suffix == "Present"
                || sections[sectionName].ContainsKey(
                    string.Format(CultureInfo.InvariantCulture, "Node{0}Present", nodeId)))
            {
                continue;
            }

            Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
                Diagnostics.ParseSeverity.Warning,
                Diagnostics.ParseDiagnosticCodes.CpjNodeEntryWithoutPresent,
                path: sectionName + "." + entry.Key,
                line: IniKeyLines.TryGetLine(sections, sectionName, entry.Key),
                rawValue: entry.Value,
                message: string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} has no Node{1}Present entry, so node {1} is not loaded. CiA 306-3 Table 3 makes NodeXPresent mandatory for each existing node. The entry is kept and written back unchanged.",
                    entry.Key,
                    nodeId)));
        }
    }

    /// <summary>
    /// Reads the mandatory <c>Nodes</c> entry (CiA 306-3 Table 3: number of nodes, 0..127, coded
    /// hexadecimal). Returns <see langword="null"/> when it is missing or invalid; the model derives
    /// the count from its nodes, so the declared value is only checked.
    /// </summary>
    private static byte? ParseDeclaredNodeCount(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName)
    {
        if (!sections[sectionName].TryGetValue("Nodes", out var raw))
        {
            Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
                Diagnostics.ParseSeverity.Warning,
                Diagnostics.ParseDiagnosticCodes.CpjMissingNodes,
                path: sectionName + ".Nodes",
                message: "The mandatory Nodes entry is missing (CiA 306-3 Table 3). The writer emits the number of nodes in the model."));
            return null;
        }

        const string ignored = "The declared count is ignored.";
        if (raw.Trim().Length == 0)
        {
            LenientIniNumber.ReportInvalid(
                sections, sectionName, "Nodes", raw, Diagnostics.ParseDiagnosticCodes.CpjInvalidNodes, null, ignored);
            return null;
        }

        var declared = LenientIniNumber.ParseOptionalByte(
            sections, sectionName, "Nodes", raw, Diagnostics.ParseDiagnosticCodes.CpjInvalidNodes, ignored);
        if (declared > CanOpenNodeId.MaxValue)
        {
            LenientIniNumber.ReportInvalid(
                sections, sectionName, "Nodes", raw, Diagnostics.ParseDiagnosticCodes.CpjInvalidNodes, null, ignored);
            return null;
        }

        if (declared != null && !raw.TrimStart().StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            LenientIniNumber.ReportInvalid(
                sections, sectionName, "Nodes", raw, Diagnostics.ParseDiagnosticCodes.CpjNodesNotHex, null,
                "CiA 306-3 Table 3 codes Nodes hexadecimal with a 0x prefix; the value is read as a plain number.");
        }

        return declared;
    }

    /// <summary>
    /// Parses <c>NodeXPresent</c>: <c>0x01</c> present, <c>0x00</c> not present, "all other values are
    /// reserved" (CiA 306-3 Table 3). The aliases <c>0</c>/<c>1</c>/<c>true</c>/<c>false</c>/<c>yes</c>/<c>no</c>
    /// stay accepted. A reserved value reads as not present; an empty value yields <see langword="null"/>
    /// (the node is not loaded). Both are reported, and strict mode throws.
    /// </summary>
    private static bool? ParsePresent(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string keyName,
        string raw)
    {
        var token = raw.Trim();
        if (token.Length == 0)
        {
            LenientIniNumber.ReportInvalid(
                sections, sectionName, keyName, raw, Diagnostics.ParseDiagnosticCodes.CpjReservedNodePresent,
                null, "The node is not loaded.");
            return null;
        }

        if (!IsKnownPresentToken(token))
        {
            LenientIniNumber.ReportInvalid(
                sections, sectionName, keyName, raw, Diagnostics.ParseDiagnosticCodes.CpjReservedNodePresent,
                "false", "Only 0x00 and 0x01 are defined; treated as not present.");
            return false;
        }

        return ValueConverter.ParsePresentFlag(token);
    }

    private static bool IsKnownPresentToken(string token)
    {
        foreach (var known in KnownPresentTokens)
        {
            if (token.Equals(known, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static readonly string[] KnownPresentTokens =
        { "0x01", "0x1", "1", "true", "yes", "0x00", "0x0", "0", "false", "no" };

    private static string? NullIfEmpty(string value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }
}

#pragma warning restore CA1846 // span-based overloads not available in netstandard2.0
