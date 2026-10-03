namespace EdsDcfNet.Tests.Parsers;

using System.Globalization;
using System.Text;
using AwesomeAssertions;
using EdsDcfNet;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using Xunit;

/// <summary>
/// CPJ <c>Nodes</c> and <c>NodeXPresent</c> (CiA 306-3 v1.2.0, section 6.2.2, Table 3):
/// <c>Nodes</c> is the number of nodes in the net, coded hexadecimal, and mandatory.
/// <c>NodeXPresent</c> is <c>0x01</c> (present) or <c>0x00</c> (not present); "all other
/// values are reserved". X is the node-ID, 1 to 127.
/// </summary>
public class CpjNodesValidationTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    private static string Cpj(params string[] lines) => string.Join("\n", lines) + "\n";

    // --- NodeXPresent ----------------------------------------------------

    [Theory]
    [InlineData("0x00", false)]
    [InlineData("0x01", true)]
    public void ReadStringWithDiagnostics_NodePresentSpecValues_AtMaxValue_ReportsNothing(string token, bool expected)
    {
        var content = Cpj("[Topology]", "Nodes=0x01", "Node1Present=" + token);

        var result = CanOpenFile.Cpj.ReadStringWithDiagnostics(content, Strict);

        result.Diagnostics.Should().BeEmpty();
        result.Model.Networks[0].Nodes[1].Present.Should().Be(expected);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("0x02")]
    [InlineData("-1")]
    [InlineData("0xFF")]
    [InlineData("maybe")]
    public void ReadStringWithDiagnostics_NodePresentReservedValue_Lenient_ReportsAndTreatsAsNotPresent(string token)
    {
        var content = Cpj("[Topology]", "Nodes=0x01", "Node1Present=" + token, "Node1Name=Drive");

        var result = CanOpenFile.Cpj.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.CpjReservedNodePresent);
        diagnostic.Severity.Should().Be(ParseSeverity.Warning);
        diagnostic.Path.Should().Be("Topology.Node1Present");
        diagnostic.Line.Should().Be(3);
        diagnostic.RawValue.Should().Be(token);
        diagnostic.CoercedTo.Should().Be("false");
        var node = result.Model.Networks[0].Nodes[1];
        node.Present.Should().BeFalse();
        node.Name.Should().Be("Drive");
    }

    [Theory]
    [InlineData("2")]
    [InlineData("-1")]
    [InlineData("maybe")]
    public void ReadStringWithDiagnostics_NodePresentReservedValue_Strict_ThrowsWithCodeSectionAndLine(string token)
    {
        var content = Cpj("[Topology]", "Nodes=0x01", "Node1Present=" + token);

        var act = () => CanOpenFile.Cpj.ReadStringWithDiagnostics(content, Strict);

        var ex = act.Should().Throw<EdsParseException>().Which;
        ex.Code.Should().Be(ParseDiagnosticCodes.CpjReservedNodePresent);
        ex.SectionName.Should().Be("Topology");
        ex.LineNumber.Should().Be(3);
    }

    [Fact]
    public void ReadStringWithDiagnostics_NodePresentEmpty_Lenient_ReportsAndDoesNotLoadNode()
    {
        var content = Cpj("[Topology]", "Nodes=0x00", "Node1Present=", "Node1Name=Drive");

        var result = CanOpenFile.Cpj.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.CpjReservedNodePresent);
        diagnostic.Path.Should().Be("Topology.Node1Present");
        diagnostic.RawValue.Should().BeEmpty();
        result.Model.Networks[0].Nodes.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_NodePresentEmpty_Strict_Throws()
    {
        var content = Cpj("[Topology]", "Nodes=0x00", "Node1Present=");

        var act = () => CanOpenFile.Cpj.ReadString(content, Strict);

        act.Should().Throw<EdsParseException>()
            .Which.Code.Should().Be(ParseDiagnosticCodes.CpjReservedNodePresent);
    }

    [Fact]
    public void ReadStringWithDiagnostics_SecondTopologyReservedPresent_PathNamesThatSection()
    {
        var content = Cpj(
            "[Topology]", "Nodes=0x01", "Node1Present=0x01",
            "[Topology2]", "Nodes=0x01", "Node7Present=2");

        var result = CanOpenFile.Cpj.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle().Which.Path.Should().Be("Topology2.Node7Present");
    }

    // --- Nodes -----------------------------------------------------------

    [Fact]
    public void ReadStringWithDiagnostics_NodesAtMaxValue_127Nodes_ReportsNothing()
    {
        var content = CpjWithNodes("0x7F", 127);

        var result = CanOpenFile.Cpj.ReadStringWithDiagnostics(content, Strict);

        result.Diagnostics.Should().BeEmpty();
        result.Model.Networks[0].Nodes.Should().HaveCount(127);
    }

    [Fact]
    public void ReadStringWithDiagnostics_NodesAboveMaxValue_0x80_Lenient_ReportsInvalidNodes()
    {
        var content = CpjWithNodes("0x80", 127);

        var result = CanOpenFile.Cpj.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.CpjInvalidNodes);
        diagnostic.Path.Should().Be("Topology.Nodes");
        diagnostic.RawValue.Should().Be("0x80");
        result.Model.Networks[0].Nodes.Should().HaveCount(127);
    }

    [Theory]
    [InlineData("0x80")]
    [InlineData("0x100")]
    [InlineData("two")]
    [InlineData("-1")]
    public void ReadString_NodesInvalid_Strict_ThrowsWithCodeSectionAndLine(string token)
    {
        var content = Cpj("[Topology]", "Nodes=" + token);

        var act = () => CanOpenFile.Cpj.ReadString(content, Strict);

        var ex = act.Should().Throw<EdsParseException>().Which;
        ex.Code.Should().Be(ParseDiagnosticCodes.CpjInvalidNodes);
        ex.SectionName.Should().Be("Topology");
        ex.LineNumber.Should().Be(2);
    }

    [Theory]
    [InlineData("two")]
    [InlineData("-1")]
    public void ReadStringWithDiagnostics_NodesMalformed_Lenient_ReportsInvalidNodesOnly(string token)
    {
        var content = Cpj("[Topology]", "Nodes=" + token, "Node2Present=0x01");

        var result = CanOpenFile.Cpj.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle().Which.Code.Should().Be(ParseDiagnosticCodes.CpjInvalidNodes);
    }

    [Fact]
    public void ReadStringWithDiagnostics_NodesMissing_ReportsInLenientAndStrictMode()
    {
        var content = Cpj("[Topology]", "NetName=Net", "Node2Present=0x01");

        var lenient = CanOpenFile.Cpj.ReadStringWithDiagnostics(content);
        var strict = CanOpenFile.Cpj.ReadStringWithDiagnostics(content, Strict);

        foreach (var result in new[] { lenient, strict })
        {
            var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
            diagnostic.Code.Should().Be(ParseDiagnosticCodes.CpjMissingNodes);
            diagnostic.Path.Should().Be("Topology.Nodes");
            result.Model.Networks[0].Nodes.Should().ContainKey(2);
        }
    }

    [Fact]
    public void ReadStringWithDiagnostics_NodesEmptyValue_ReportsInvalidNodes()
    {
        var content = Cpj("[Topology]", "Nodes=", "Node2Present=0x01");

        var result = CanOpenFile.Cpj.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle().Which.Code.Should().Be(ParseDiagnosticCodes.CpjInvalidNodes);
    }

    [Fact]
    public void ReadStringWithDiagnostics_NodesDiffersFromNodeCount_ReportsMismatchWithoutThrowing()
    {
        var content = Cpj("[Topology]", "Nodes=0x05", "Node2Present=0x01", "Node3Present=0x00");

        var lenient = CanOpenFile.Cpj.ReadStringWithDiagnostics(content);
        var strict = CanOpenFile.Cpj.ReadStringWithDiagnostics(content, Strict);

        foreach (var result in new[] { lenient, strict })
        {
            var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
            diagnostic.Code.Should().Be(ParseDiagnosticCodes.CpjNodeCountMismatch);
            diagnostic.Path.Should().Be("Topology.Nodes");
            diagnostic.RawValue.Should().Be("0x05");
            result.Model.Networks[0].Nodes.Should().HaveCount(2);
        }
    }

    [Fact]
    public void ReadStringWithDiagnostics_CiA3063Figure8Example_ReportsNothing()
    {
        // CiA 306-3 v1.2.0, Figure 8 (nodelist.cpj with minimum information).
        var content = Cpj(
            "[Topology]", "Nodes=0x03",
            "Node2Present=0x01", "Node2DCFName=demo_plc.dcf",
            "Node3Present=0x01", "Node3DCFName=demodeva.dcf",
            "Node4Present=0x01", "Node4DCFName=demodevb.dcf");

        var result = CanOpenFile.Cpj.ReadStringWithDiagnostics(content, Strict);

        result.Diagnostics.Should().BeEmpty();
        result.Model.Networks[0].Nodes.Keys.Should().BeEquivalentTo(new byte[] { 2, 3, 4 });
    }

    // --- Round trip ------------------------------------------------------

    [Fact]
    public void ReadWriteRead_NodesAndNodePresent_ValidatedRoundTrip()
    {
        // CiA 306-3 Table 3: Nodes (hex count) and NodeXPresent (0x00/0x01) stay as read.
        var content = Cpj(
            "[Topology]", "Nodes=0x03",
            "Node2Present=0x01", "Node3Present=0x00", "Node127Present=0x01");
        var first = CanOpenFile.Cpj.ReadString(content, Strict);

        var written = CanOpenFile.Cpj.WriteToString(first);
        var second = CanOpenFile.Cpj.ReadStringWithDiagnostics(written, Strict);

        written.Should().Contain("Nodes=0x03");
        written.Should().Contain("Node2Present=0x01");
        written.Should().Contain("Node3Present=0x00");
        written.Should().Contain("Node127Present=0x01");
        second.Diagnostics.Should().BeEmpty();
        second.Model.Networks[0].Nodes.Select(n => (n.Key, n.Value.Present))
            .Should().BeEquivalentTo(first.Networks[0].Nodes.Select(n => (n.Key, n.Value.Present)));
    }

    [Fact]
    public void ReadWriteRead_127Nodes_AtMaxValue_KeepsNodesCount()
    {
        var first = CanOpenFile.Cpj.ReadString(CpjWithNodes("0x7F", 127), Strict);

        var written = CanOpenFile.Cpj.WriteToString(first);
        var second = CanOpenFile.Cpj.ReadStringWithDiagnostics(written, Strict);

        written.Should().Contain("Nodes=0x7F");
        second.Diagnostics.Should().BeEmpty();
        second.Model.Networks[0].Nodes.Should().HaveCount(127);
    }

    private static string CpjWithNodes(string nodesToken, int nodeCount)
    {
        var sb = new StringBuilder();
        sb.Append("[Topology]\nNodes=").Append(nodesToken).Append('\n');
        for (var id = 1; id <= nodeCount; id++)
        {
            sb.Append("Node").Append(id.ToString(CultureInfo.InvariantCulture)).Append("Present=0x01\n");
        }

        return sb.ToString();
    }
}
