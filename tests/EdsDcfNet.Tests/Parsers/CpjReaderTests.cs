namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using FluentAssertions;
using Xunit;

public class CpjReaderTests
{
    private readonly CpjReader _reader = new();

    [Fact]
    public void ReadString_SimpleTopologyWith3Nodes_ParsesCorrectly()
    {
        // Arrange
        var content = @"
[Topology]
NetName=Production Line 1
NetRefd=N1
Nodes=0x03
Node2Present=0x01
Node2Name=PLC
Node2DCFName=demo_plc.dcf
Node3Present=0x01
Node3Name=IO Module A
Node3DCFName=demodeva.dcf
Node4Present=0x01
Node4Name=IO Module B
Node4DCFName=demodevb.dcf
EDSBaseName=/eds/
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Networks.Should().HaveCount(1);
        var network = result.Networks[0];
        network.NetName.Should().Be("Production Line 1");
        network.NetRefd.Should().Be("N1");
        network.EdsBaseName.Should().Be("/eds/");
        network.Nodes.Should().HaveCount(3);

        network.Nodes[2].NodeId.Should().Be(2);
        network.Nodes[2].Present.Should().BeTrue();
        network.Nodes[2].Name.Should().Be("PLC");
        network.Nodes[2].DcfFileName.Should().Be("demo_plc.dcf");

        network.Nodes[3].Name.Should().Be("IO Module A");
        network.Nodes[3].DcfFileName.Should().Be("demodeva.dcf");

        network.Nodes[4].Name.Should().Be("IO Module B");
        network.Nodes[4].DcfFileName.Should().Be("demodevb.dcf");
    }

    [Fact]
    public void ReadString_MultipleTopologySections_ParsesMultipleNetworks()
    {
        // Arrange
        var content = @"
[Topology]
NetName=Network A
Nodes=0x01
Node1Present=0x01
Node1Name=Device A
Node1DCFName=a.dcf

[Topology2]
NetName=Network B
Nodes=0x01
Node5Present=0x01
Node5Name=Device B
Node5DCFName=b.dcf
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Networks.Should().HaveCount(2);
        result.Networks[0].NetName.Should().Be("Network A");
        result.Networks[0].Nodes.Should().HaveCount(1);
        result.Networks[0].Nodes[1].Name.Should().Be("Device A");

        result.Networks[1].NetName.Should().Be("Network B");
        result.Networks[1].Nodes.Should().HaveCount(1);
        result.Networks[1].Nodes[5].Name.Should().Be("Device B");
    }

    [Fact]
    public void ReadString_MissingOptionalFields_SetsNulls()
    {
        // Arrange
        var content = @"
[Topology]
Nodes=0x01
Node1Present=0x01
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var network = result.Networks[0];
        network.NetName.Should().BeNull();
        network.NetRefd.Should().BeNull();
        network.EdsBaseName.Should().BeNull();
        network.Nodes[1].Name.Should().BeNull();
        network.Nodes[1].Refd.Should().BeNull();
        network.Nodes[1].DcfFileName.Should().BeNull();
    }

    [Fact]
    public void ReadString_NodeWithPresentZero_ParsesAsNotPresent()
    {
        // Arrange
        var content = @"
[Topology]
Nodes=0x02
Node1Present=0x01
Node1Name=Active
Node2Present=0x00
Node2Name=Inactive
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var network = result.Networks[0];
        network.Nodes[1].Present.Should().BeTrue();
        network.Nodes[2].Present.Should().BeFalse();
        network.Nodes[2].Name.Should().Be("Inactive");
    }

    [Fact]
    public void ReadString_NonSequentialNodeIds_ParsesCorrectly()
    {
        // Arrange
        var content = @"
[Topology]
Nodes=0x03
Node2Present=0x01
Node2Name=Node Two
Node5Present=0x01
Node5Name=Node Five
Node127Present=0x01
Node127Name=Node Max
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var network = result.Networks[0];
        network.Nodes.Should().HaveCount(3);
        network.Nodes.Should().ContainKeys(2, 5, 127);
        network.Nodes[2].Name.Should().Be("Node Two");
        network.Nodes[5].Name.Should().Be("Node Five");
        network.Nodes[127].Name.Should().Be("Node Max");
    }

    [Fact]
    public void ReadString_UnknownSections_GoToAdditionalSections()
    {
        // Arrange
        var content = @"
[Topology]
NetName=Net1
Nodes=0x00

[ProjectInfo]
ProjectName=MyProject
Version=1.0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Networks.Should().HaveCount(1);
        result.AdditionalSections.Should().ContainKey("ProjectInfo");
        result.AdditionalSections["ProjectInfo"]["ProjectName"].Should().Be("MyProject");
        result.AdditionalSections["ProjectInfo"]["Version"].Should().Be("1.0");
    }

    [Fact]
    public void ReadString_NodeWithRefd_ParsesRefdField()
    {
        // Arrange
        var content = @"
[Topology]
Nodes=0x01
Node10Present=0x01
Node10Name=Sensor
Node10Refd=S1
Node10DCFName=sensor.dcf
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var node = result.Networks[0].Nodes[10];
        node.Refd.Should().Be("S1");
    }

    [Fact]
    public void ReadString_EmptyContent_ReturnsEmptyProject()
    {
        // Arrange
        var content = "";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Networks.Should().BeEmpty();
        result.AdditionalSections.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_NodePresentWithDecimalOne_ParsesAsPresent()
    {
        // Arrange
        var content = @"
[Topology]
Nodes=0x01
Node1Present=1
Node1Name=DecimalPresent
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Networks[0].Nodes[1].Present.Should().BeTrue();
    }
}
