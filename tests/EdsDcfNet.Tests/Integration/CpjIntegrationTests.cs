namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Models;
using FluentAssertions;
using Xunit;

public class CpjIntegrationTests
{
    [Fact]
    public void ReadCpjFromString_ValidContent_ReturnsNodelistProject()
    {
        // Arrange
        var content = @"
[Topology]
NetName=Test Network
Nodes=0x02
Node1Present=0x01
Node1Name=Controller
Node1DCFName=ctrl.dcf
Node10Present=0x01
Node10Name=Sensor
Node10DCFName=sensor.dcf
EDSBaseName=/config/
";

        // Act
        var result = CanOpenFile.ReadCpjFromString(content);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NodelistProject>();
        result.Networks.Should().HaveCount(1);
        result.Networks[0].NetName.Should().Be("Test Network");
        result.Networks[0].Nodes.Should().HaveCount(2);
    }

    [Fact]
    public void WriteCpjToString_ValidProject_ProducesCorrectOutput()
    {
        // Arrange
        var project = new NodelistProject();
        project.Networks.Add(new NetworkTopology
        {
            NetName = "My Network",
            Nodes =
            {
                [3] = new NetworkNode { NodeId = 3, Present = true, Name = "Drive", DcfFileName = "drive.dcf" }
            }
        });

        // Act
        var result = CanOpenFile.WriteCpjToString(project);

        // Assert
        result.Should().Contain("[Topology]");
        result.Should().Contain("NetName=My Network");
        result.Should().Contain("Node3Present=0x01");
        result.Should().Contain("Node3Name=Drive");
        result.Should().Contain("Node3DCFName=drive.dcf");
    }

    [Fact]
    public void WriteCpj_ReadCpj_RoundTrip_ViaFile()
    {
        // Arrange
        var project = new NodelistProject();
        project.Networks.Add(new NetworkTopology
        {
            NetName = "File Test",
            NetRefd = "FT1",
            Nodes =
            {
                [1] = new NetworkNode { NodeId = 1, Present = true, Name = "Node1", DcfFileName = "n1.dcf" }
            }
        });

        var tempFile = Path.GetTempFileName();
        try
        {
            // Act
            CanOpenFile.WriteCpj(project, tempFile);
            var result = CanOpenFile.ReadCpj(tempFile);

            // Assert
            result.Networks.Should().HaveCount(1);
            result.Networks[0].NetName.Should().Be("File Test");
            result.Networks[0].NetRefd.Should().Be("FT1");
            result.Networks[0].Nodes[1].Name.Should().Be("Node1");
            result.Networks[0].Nodes[1].DcfFileName.Should().Be("n1.dcf");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadCpj_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Act
        var act = () => CanOpenFile.ReadCpj("NonExistent.cpj");

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }
}
