namespace EdsDcfNet.Tests.Writers;

using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Writers;
using FluentAssertions;
using Xunit;

public class CpjWriterTests
{
    private readonly CpjWriter _writer = new();
    private readonly CpjReader _reader = new();

    [Fact]
    public void RoundTrip_ParseWriteParse_PreservesAllData()
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
        var parsed = _reader.ReadString(content);
        var written = _writer.GenerateString(parsed);
        var reparsed = _reader.ReadString(written);

        // Assert
        reparsed.Networks.Should().HaveCount(1);
        var network = reparsed.Networks[0];
        network.NetName.Should().Be("Production Line 1");
        network.NetRefd.Should().Be("N1");
        network.EdsBaseName.Should().Be("/eds/");
        network.Nodes.Should().HaveCount(3);
        network.Nodes[2].Name.Should().Be("PLC");
        network.Nodes[2].DcfFileName.Should().Be("demo_plc.dcf");
        network.Nodes[2].Present.Should().BeTrue();
        network.Nodes[3].Name.Should().Be("IO Module A");
        network.Nodes[4].Name.Should().Be("IO Module B");
    }

    [Fact]
    public void GenerateString_EmptyProject_ProducesEmptyOutput()
    {
        // Arrange
        var project = new NodelistProject();

        // Act
        var result = _writer.GenerateString(project);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateString_MultipleNetworks_WritesMultipleTopologySections()
    {
        // Arrange
        var project = new NodelistProject();
        project.Networks.Add(new NetworkTopology
        {
            NetName = "Net A",
            Nodes = { [1] = new NetworkNode { NodeId = 1, Present = true, Name = "A1" } }
        });
        project.Networks.Add(new NetworkTopology
        {
            NetName = "Net B",
            Nodes = { [5] = new NetworkNode { NodeId = 5, Present = true, Name = "B5" } }
        });

        // Act
        var result = _writer.GenerateString(project);

        // Assert
        result.Should().Contain("[Topology]");
        result.Should().Contain("[Topology2]");
        result.Should().Contain("NetName=Net A");
        result.Should().Contain("NetName=Net B");

        // Round-trip verify
        var reparsed = _reader.ReadString(result);
        reparsed.Networks.Should().HaveCount(2);
        reparsed.Networks[0].NetName.Should().Be("Net A");
        reparsed.Networks[1].NetName.Should().Be("Net B");
    }

    [Fact]
    public void GenerateString_NodeNotPresent_WritesPresent0x00()
    {
        // Arrange
        var project = new NodelistProject();
        project.Networks.Add(new NetworkTopology
        {
            Nodes = { [1] = new NetworkNode { NodeId = 1, Present = false, Name = "Offline" } }
        });

        // Act
        var result = _writer.GenerateString(project);

        // Assert
        result.Should().Contain("Node1Present=0x00");
        result.Should().Contain("Node1Name=Offline");
    }

    [Fact]
    public void GenerateString_NodesOrderedByNodeId()
    {
        // Arrange
        var project = new NodelistProject();
        var topology = new NetworkTopology();
        topology.Nodes[10] = new NetworkNode { NodeId = 10, Present = true, Name = "Ten" };
        topology.Nodes[3] = new NetworkNode { NodeId = 3, Present = true, Name = "Three" };
        topology.Nodes[1] = new NetworkNode { NodeId = 1, Present = true, Name = "One" };
        project.Networks.Add(topology);

        // Act
        var result = _writer.GenerateString(project);

        // Assert
        var idx1 = result.IndexOf("Node1Present");
        var idx3 = result.IndexOf("Node3Present");
        var idx10 = result.IndexOf("Node10Present");
        idx1.Should().BeLessThan(idx3);
        idx3.Should().BeLessThan(idx10);
    }

    [Fact]
    public void RoundTrip_MultipleNetworks_PreservesAllData()
    {
        // Arrange
        var project = new NodelistProject();
        project.Networks.Add(new NetworkTopology
        {
            NetName = "First",
            NetRefd = "N1",
            EdsBaseName = "/path1/",
            Nodes =
            {
                [1] = new NetworkNode { NodeId = 1, Present = true, Name = "Dev1", DcfFileName = "d1.dcf", Refd = "R1" },
                [2] = new NetworkNode { NodeId = 2, Present = false }
            }
        });
        project.Networks.Add(new NetworkTopology
        {
            NetName = "Second",
            Nodes =
            {
                [127] = new NetworkNode { NodeId = 127, Present = true, Name = "MaxNode" }
            }
        });

        // Act
        var written = _writer.GenerateString(project);
        var reparsed = _reader.ReadString(written);

        // Assert
        reparsed.Networks.Should().HaveCount(2);

        var net1 = reparsed.Networks[0];
        net1.NetName.Should().Be("First");
        net1.NetRefd.Should().Be("N1");
        net1.EdsBaseName.Should().Be("/path1/");
        net1.Nodes[1].Present.Should().BeTrue();
        net1.Nodes[1].Name.Should().Be("Dev1");
        net1.Nodes[1].DcfFileName.Should().Be("d1.dcf");
        net1.Nodes[1].Refd.Should().Be("R1");
        net1.Nodes[2].Present.Should().BeFalse();

        var net2 = reparsed.Networks[1];
        net2.NetName.Should().Be("Second");
        net2.Nodes[127].Name.Should().Be("MaxNode");
    }

    [Fact]
    public void GenerateString_AdditionalSections_AreWritten()
    {
        // Arrange
        var project = new NodelistProject();
        project.AdditionalSections["CustomSection"] = new Dictionary<string, string>
        {
            ["Key1"] = "Value1",
            ["Key2"] = "Value2"
        };

        // Act
        var result = _writer.GenerateString(project);

        // Assert
        result.Should().Contain("[CustomSection]");
        result.Should().Contain("Key1=Value1");
        result.Should().Contain("Key2=Value2");
    }

    [Fact]
    public void WriteFile_NonAsciiCharacters_PreservesCharacters()
    {
        // Arrange
        var project = new NodelistProject();
        var network = new NetworkTopology { NetName = "Netzwerk Süd" };
        network.Nodes[1] = new NetworkNode { NodeId = 1, Present = true, Name = "Antrieb Ü1" };
        project.Networks.Add(network);
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            _writer.WriteFile(project, tempFile);

            // Assert
            var content = File.ReadAllText(tempFile, System.Text.Encoding.UTF8);
            content.Should().Contain("Netzwerk Süd");
            content.Should().Contain("Antrieb Ü1");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
