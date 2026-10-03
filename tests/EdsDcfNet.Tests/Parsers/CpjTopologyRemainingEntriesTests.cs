namespace EdsDcfNet.Tests.Parsers;

using AwesomeAssertions;
using EdsDcfNet;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using Xunit;

/// <summary>
/// CiA 306-3 v1.2.0, section 6.2.2, Table 3 defines the [Topology] entries NetName, NetRefd, Nodes,
/// NodeXPresent, NodeXName, NodeXRefd, NodeXDCFName and EDSBaseName; section 6.2 leaves other entries
/// to the tool manufacturers. A "missing entry" NodeXPresent means "not present", so a NodeXName
/// without NodeXPresent is valid but incomplete information. Neither may be dropped.
/// </summary>
public class CpjTopologyRemainingEntriesTests
{
    private static readonly CanOpenFileOptions Strict = new() { StrictParsing = true };

    private static string Cpj(params string[] lines) => string.Join("\n", lines) + "\n";

    [Fact]
    public void ReadString_VendorKeyInTopology_IsKeptInRemainingEntries()
    {
        var content = Cpj("[Topology]", "NetName=Net", "Nodes=0x01", "Node1Present=0x01", "VendorColor=blue", "EDSBaseName=C:\\E");

        var cpj = CanOpenFile.Cpj.ReadString(content);

        var remaining = cpj.Networks[0].RemainingEntries;
        remaining.Should().ContainSingle();
        remaining["VendorColor"].Should().Be("blue");
    }

    [Fact]
    public void ReadString_KnownTopologyKeys_AreNotRemainingEntries()
    {
        var content = Cpj(
            "[Topology]", "NetName=Net", "NetRefd=R", "Nodes=0x01", "Node1Present=0x01", "Node1Name=A",
            "Node1Refd=B", "Node1DCFName=a.dcf", "EDSBaseName=C:\\E");

        var cpj = CanOpenFile.Cpj.ReadString(content);

        cpj.Networks[0].RemainingEntries.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_VendorKey_RoundTripsInOriginalOrderAfterGeneratedKeys()
    {
        var content = Cpj("[Topology]", "ZVendor=1", "NetName=Net", "Nodes=0x01", "Node1Present=0x01", "AVendor=2");

        var cpj = CanOpenFile.Cpj.ReadString(content);
        var written = CanOpenFile.Cpj.WriteToString(cpj);
        var reread = CanOpenFile.Cpj.ReadString(written);

        written.IndexOf("ZVendor=1", StringComparison.Ordinal).Should().BeGreaterThan(written.IndexOf("Node1Present=0x01", StringComparison.Ordinal));
        written.IndexOf("AVendor=2", StringComparison.Ordinal).Should().BeGreaterThan(written.IndexOf("ZVendor=1", StringComparison.Ordinal));
        reread.Networks[0].RemainingEntries.Keys.Should().Equal("ZVendor", "AVendor");
        reread.Networks[0].Nodes.Should().ContainKey(1);
    }

    [Fact]
    public void ReadString_NodeNameWithoutPresent_IsKeptVerbatimAndNoNodeIsLoaded()
    {
        var content = Cpj("[Topology]", "Nodes=0x00", "Node5Name=Drive", "Node5DCFName=d.dcf");

        var cpj = CanOpenFile.Cpj.ReadString(content);

        var topology = cpj.Networks[0];
        topology.Nodes.Should().BeEmpty();
        topology.RemainingEntries["Node5Name"].Should().Be("Drive");
        topology.RemainingEntries["Node5DCFName"].Should().Be("d.dcf");
    }

    [Fact]
    public void ReadString_NodeNameWithoutPresent_RoundTripsThroughWriter()
    {
        var content = Cpj("[Topology]", "Nodes=0x00", "Node5Name=Drive", "Node5DCFName=d.dcf");

        var written = CanOpenFile.Cpj.WriteToString(CanOpenFile.Cpj.ReadString(content));
        var reread = CanOpenFile.Cpj.ReadString(written);

        written.Should().Contain("Node5Name=Drive");
        written.Should().Contain("Node5DCFName=d.dcf");
        reread.Networks[0].RemainingEntries["Node5Name"].Should().Be("Drive");
        reread.Networks[0].Nodes.Should().BeEmpty();
    }

    [Fact]
    public void ReadStringWithDiagnostics_NodeNameWithoutPresent_Lenient_ReportsWarningPerKey()
    {
        var content = Cpj("[Topology]", "Nodes=0x00", "Node5Name=Drive");

        var result = CanOpenFile.Cpj.ReadStringWithDiagnostics(content);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(ParseDiagnosticCodes.CpjNodeEntryWithoutPresent);
        diagnostic.Severity.Should().Be(ParseSeverity.Warning);
        diagnostic.Path.Should().Be("Topology.Node5Name");
        diagnostic.Line.Should().Be(3);
        diagnostic.RawValue.Should().Be("Drive");
    }

    [Fact]
    public void ReadStringWithDiagnostics_NodeNameWithoutPresent_Strict_DoesNotThrow()
    {
        var content = Cpj("[Topology]", "Nodes=0x00", "Node5Name=Drive", "Node6Refd=R");

        var result = CanOpenFile.Cpj.ReadStringWithDiagnostics(content, Strict);

        result.Diagnostics.Should().HaveCount(2).And.OnlyContain(d => d.Code == ParseDiagnosticCodes.CpjNodeEntryWithoutPresent);
        result.Model.Networks[0].RemainingEntries.Keys.Should().Equal("Node5Name", "Node6Refd");
    }

    [Fact]
    public void ReadStringWithDiagnostics_NodeNameWithPresent_ReportsNothing()
    {
        var content = Cpj("[Topology]", "Nodes=0x01", "Node5Present=0x01", "Node5Name=Drive");

        var result = CanOpenFile.Cpj.ReadStringWithDiagnostics(content, Strict);

        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ReadStringWithDiagnostics_NodeNameWithEmptyPresent_KeepsEntriesWithoutSecondDiagnostic()
    {
        var content = Cpj("[Topology]", "Nodes=0x00", "Node5Present=", "Node5Name=Drive");

        var result = CanOpenFile.Cpj.ReadStringWithDiagnostics(content);

        result.Diagnostics.Should().ContainSingle().Which.Code.Should().Be(ParseDiagnosticCodes.CpjReservedNodePresent);
        result.Model.Networks[0].RemainingEntries.Keys.Should().Equal("Node5Present", "Node5Name");
    }

    [Theory]
    [InlineData("Node0Name")]
    [InlineData("Node128Name")]
    [InlineData("Node01Name")]
    [InlineData("NodeXName")]
    [InlineData("Node1Color")]
    [InlineData("Node-1Name")]
    [InlineData("Node5")]
    [InlineData("Node")]
    public void ReadString_NodeKeyOutsideTable3_IsKeptAsRemainingEntry(string key)
    {
        var content = Cpj("[Topology]", "Nodes=0x01", "Node1Present=0x01", key + "=v");

        var cpj = CanOpenFile.Cpj.ReadString(content);

        cpj.Networks[0].RemainingEntries[key].Should().Be("v");
        cpj.Networks[0].Nodes.Should().ContainKey(1);
    }

    [Fact]
    public void ReadString_TwoTopologiesWithSameVendorKey_KeepEachValue()
    {
        var content = Cpj(
            "[Topology]", "Nodes=0x00", "Vendor=first", "Node3Name=A",
            "[Topology2]", "Nodes=0x00", "Vendor=second", "Node3Name=B");

        var written = CanOpenFile.Cpj.WriteToString(CanOpenFile.Cpj.ReadString(content));
        var reread = CanOpenFile.Cpj.ReadString(written);

        reread.Networks.Should().HaveCount(2);
        reread.Networks[0].RemainingEntries["Vendor"].Should().Be("first");
        reread.Networks[0].RemainingEntries["Node3Name"].Should().Be("A");
        reread.Networks[1].RemainingEntries["Vendor"].Should().Be("second");
        reread.Networks[1].RemainingEntries["Node3Name"].Should().Be("B");
    }

    [Fact]
    public void WriteToString_RemainingEntryCollidesWithGeneratedKey_GeneratedKeyWins()
    {
        var topology = new NetworkTopology { NetName = "Generated", EdsBaseName = "C:\\E" };
        topology.Nodes[1] = new NetworkNode { NodeId = 1, Present = true, Name = "Real" };
        topology.RemainingEntries.Add("NetName", "Stale");
        topology.RemainingEntries.Add("nodes", "0x7F");
        topology.RemainingEntries.Add("Node1Name", "Stale");
        topology.RemainingEntries.Add("Node1Present", "0x00");
        topology.RemainingEntries.Add("EDSBaseName", "Stale");
        topology.RemainingEntries.Add("Vendor", "kept");
        var cpj = new NodelistProject();
        cpj.Networks.Add(topology);

        var written = CanOpenFile.Cpj.WriteToString(cpj);

        written.Should().Contain("NetName=Generated").And.NotContain("Stale");
        written.Should().Contain("Node1Name=Real").And.Contain("Node1Present=0x01");
        written.Should().NotContain("0x7F").And.NotContain("0x00");
        written.Should().Contain("Vendor=kept");
        CountOccurrences(written, "NetName=").Should().Be(1);
    }

    [Fact]
    public void WriteToString_OrphanNodeNameAndNodeAddedLater_GeneratedNodeWins()
    {
        var cpj = CanOpenFile.Cpj.ReadString(Cpj("[Topology]", "Nodes=0x00", "Node5Name=Orphan"));
        cpj.Networks[0].Nodes[5] = new NetworkNode { NodeId = 5, Present = true };

        var written = CanOpenFile.Cpj.WriteToString(cpj);

        written.Should().Contain("Node5Present=0x01").And.NotContain("Orphan");
    }

    [Fact]
    public void WriteToString_NodeIdDiffersFromDictionaryKey_CollisionUsesEmittedNodeId()
    {
        var topology = new NetworkTopology();
        topology.Nodes[2] = new NetworkNode { NodeId = 3, Present = true, Name = "Real" };
        topology.RemainingEntries.Add("Node2Name", "Kept");
        topology.RemainingEntries.Add("Node3Name", "Stale");
        var cpj = new NodelistProject();
        cpj.Networks.Add(topology);

        var written = CanOpenFile.Cpj.WriteToString(cpj);

        written.Should().Contain("Node3Name=Real").And.Contain("Node2Name=Kept").And.NotContain("Stale");
        CountOccurrences(written, "Node3Name=").Should().Be(1);
    }

    [Fact]
    public void WriteToString_ValidatedRemainingValueWithLineBreak_ThrowsModelValidationException()
    {
        var topology = new NetworkTopology();
        topology.RemainingEntries.Add("Vendor", "a\nb");
        var cpj = new NodelistProject();
        cpj.Networks.Add(topology);

        var act = () => CanOpenFile.Cpj.WriteToString(cpj, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>().Which.Issues.Should().Contain(issue =>
            issue.Path == "Networks[0].RemainingEntries[Vendor]");
    }

    [Fact]
    public void WriteToString_ValidatedRemainingKeyEmpty_ThrowsModelValidationException()
    {
        var topology = new NetworkTopology();
        topology.RemainingEntries.Add("", "v");
        var cpj = new NodelistProject();
        cpj.Networks.Add(topology);

        var act = () => CanOpenFile.Cpj.WriteToString(cpj, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>();
    }

    [Fact]
    public void WriteToString_ValidatedSuppressedRemainingEntryWithLineBreak_Succeeds()
    {
        var topology = new NetworkTopology { NetName = "N" };
        topology.RemainingEntries.Add("NetName", "a\nb");
        var cpj = new NodelistProject();
        cpj.Networks.Add(topology);

        var written = CanOpenFile.Cpj.WriteToString(cpj, CanOpenWriteOptions.Validated);

        written.Should().Contain("NetName=N");
    }

    [Fact]
    public void WriteToString_UnvalidatedRemainingValueWithLineBreak_StillRejectedByWriter()
    {
        var topology = new NetworkTopology();
        topology.RemainingEntries.Add("Vendor", "a\nb");
        var cpj = new NodelistProject();
        cpj.Networks.Add(topology);

        var act = () => CanOpenFile.Cpj.WriteToString(cpj);

        act.Should().Throw<CpjWriteException>();
    }

    [Fact]
    public void WriteToString_ValidatedVendorKeyRoundTrip_ValidatedRoundTrip()
    {
        var content = Cpj("[Topology]", "Nodes=0x01", "Node1Present=0x01", "Vendor=x", "Node9Name=Orphan");

        var written = CanOpenFile.Cpj.WriteToString(CanOpenFile.Cpj.ReadString(content), CanOpenWriteOptions.Validated);

        written.Should().Contain("Vendor=x").And.Contain("Node9Name=Orphan");
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
