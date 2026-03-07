namespace EdsDcfNet.Tests.Writers;

using System.Reflection;
using EdsDcfNet.Exceptions;
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
    public void GenerateString_AdditionalSections_AreWrittenDeterministically()
    {
        // Arrange
        var project = new NodelistProject();
        project.AdditionalSections["zSection"] = new Dictionary<string, string>
        {
            ["zKey"] = "Z",
            ["AKey"] = "A"
        };
        project.AdditionalSections["ASection"] = new Dictionary<string, string>
        {
            ["bKey"] = "B",
            ["aKey"] = "A"
        };

        // Act
        var result = _writer.GenerateString(project);

        // Assert
        var aSectionIndex = result.IndexOf("[ASection]", StringComparison.Ordinal);
        var zSectionIndex = result.IndexOf("[zSection]", StringComparison.Ordinal);
        aSectionIndex.Should().BeGreaterThanOrEqualTo(0);
        zSectionIndex.Should().BeGreaterThanOrEqualTo(0);
        aSectionIndex.Should().BeLessThan(zSectionIndex);

        var aSectionStart = aSectionIndex;
        aSectionStart.Should().BeGreaterThanOrEqualTo(0);
        var aKeyPos = result.IndexOf("aKey=A", aSectionStart, StringComparison.Ordinal);
        var bKeyPos = result.IndexOf("bKey=B", aSectionStart, StringComparison.Ordinal);
        aKeyPos.Should().BeGreaterThanOrEqualTo(0);
        bKeyPos.Should().BeGreaterThanOrEqualTo(0);
        aKeyPos.Should().BeLessThan(bKeyPos);
    }

    [Fact]
    public void GenerateString_NullTopologyEntry_ThrowsCpjWriteExceptionWithSectionName()
    {
        // Arrange
        var project = new NodelistProject();
        project.Networks.Add(null!);

        // Act
        var act = () => _writer.GenerateString(project);

        // Assert
        var ex = act.Should().Throw<CpjWriteException>().Which;
        ex.SectionName.Should().Be("Topology");
        ex.Message.Should().Contain("Topology");
    }

    [Fact]
    public void WriteSection_WhenActionThrowsCpjWriteException_RethrowsOriginal()
    {
        var method = typeof(CpjWriter).GetMethod("WriteSection", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var expected = new CpjWriteException("forced", "Topology");

        var act = () => method!.Invoke(
            null,
            new object[] { "Topology", (Action)(() => throw expected) });

        var tie = act.Should().Throw<TargetInvocationException>().Which;
        tie.InnerException.Should().BeSameAs(expected);
    }

    [Fact]
    public void WriteFile_InvalidPath_ThrowsCpjWriteException()
    {
        // Arrange
        var project = new NodelistProject();
        project.Networks.Add(new NetworkTopology());
        var invalidPath = "/invalid/path/that/does/not/exist/test.cpj";

        // Act
        var act = () => _writer.WriteFile(project, invalidPath);

        // Assert
        act.Should().Throw<CpjWriteException>()
            .WithMessage("*Failed to write CPJ file*");
    }

    [Fact]
    public void WriteFile_GenerationThrowsCpjWriteException_Rethrows()
    {
        var project = new NodelistProject();
        project.Networks.Add(null!);
        var tempFile = Path.GetTempFileName();

        try
        {
            var act = () => _writer.WriteFile(project, tempFile);

            var ex = act.Should().Throw<CpjWriteException>().Which;
            ex.SectionName.Should().Be("Topology");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteFileAsync_InvalidPath_ThrowsCpjWriteException()
    {
        var project = new NodelistProject();
        project.Networks.Add(new NetworkTopology());
        var invalidPath = "/invalid/path/that/does/not/exist/async.cpj";

        var act = () => _writer.WriteFileAsync(project, invalidPath);

        (await act.Should().ThrowAsync<CpjWriteException>())
            .WithMessage("*Failed to write CPJ file*");
    }

    [Fact]
    public async Task WriteFileAsync_Cancelled_ThrowsOperationCanceledException()
    {
        var project = new NodelistProject();
        project.Networks.Add(new NetworkTopology());
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".cpj");

        try
        {
            var act = () => _writer.WriteFileAsync(project, tempFile, cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteFileAsync_GenerationThrowsCpjWriteException_Rethrows()
    {
        var project = new NodelistProject();
        project.Networks.Add(null!);
        var tempFile = Path.GetTempFileName();

        try
        {
            var act = () => _writer.WriteFileAsync(project, tempFile);

            var ex = (await act.Should().ThrowAsync<CpjWriteException>()).Which;
            ex.SectionName.Should().Be("Topology");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteStream_RoundTripsAndLeavesStreamOpen()
    {
        var project = new NodelistProject();
        project.Networks.Add(new NetworkTopology
        {
            NetName = "Stream Net",
            Nodes =
            {
                [2] = new NetworkNode { NodeId = 2, Present = true, Name = "Drive" }
            }
        });

        using var stream = new MemoryStream();
        _writer.WriteStream(project, stream);
        stream.CanWrite.Should().BeTrue();
        stream.Position = 0;

        var parsed = _reader.ReadStream(stream);
        parsed.Networks.Should().ContainSingle();
        parsed.Networks[0].NetName.Should().Be("Stream Net");
    }

    [Fact]
    public async Task WriteStreamAsync_RoundTripsAndLeavesStreamOpen()
    {
        var project = new NodelistProject();
        project.Networks.Add(new NetworkTopology
        {
            NetName = "Stream Net",
            Nodes =
            {
                [2] = new NetworkNode { NodeId = 2, Present = true, Name = "Drive" }
            }
        });

        using var stream = new MemoryStream();
        await _writer.WriteStreamAsync(project, stream);
        stream.CanWrite.Should().BeTrue();
        stream.Position = 0;

        var parsed = await _reader.ReadStreamAsync(stream);
        parsed.Networks.Should().ContainSingle();
        parsed.Networks[0].NetName.Should().Be("Stream Net");
    }

    [Fact]
    public void WriteStream_UnwritableStream_ThrowsArgumentException()
    {
        var project = new NodelistProject();
        project.Networks.Add(new NetworkTopology());
        using var stream = new MemoryStream(new byte[16], writable: false);

        var act = () => _writer.WriteStream(project, stream);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("stream");
    }

    [Fact]
    public async Task WriteStreamAsync_UnwritableStream_ThrowsArgumentException()
    {
        var project = new NodelistProject();
        project.Networks.Add(new NetworkTopology());
        using var stream = new MemoryStream(new byte[16], writable: false);

        var act = () => _writer.WriteStreamAsync(project, stream);

        await act.Should().ThrowAsync<ArgumentException>()
            .Where(ex => ex.ParamName == "stream");
    }

    [Fact]
    public void WriteStream_NullStream_ThrowsArgumentNullException()
    {
        var project = new NodelistProject();
        project.Networks.Add(new NetworkTopology());

        var act = () => _writer.WriteStream(project, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("stream");
    }

    [Fact]
    public void WriteStream_GenerationThrowsCpjWriteException_Rethrows()
    {
        var project = new NodelistProject();
        project.Networks.Add(null!);
        using var stream = new MemoryStream();

        var act = () => _writer.WriteStream(project, stream);

        var ex = act.Should().Throw<CpjWriteException>().Which;
        ex.SectionName.Should().Be("Topology");
    }

    [Fact]
    public void WriteStream_StreamWriteThrows_WrapsInCpjWriteException()
    {
        var project = new NodelistProject();
        project.Networks.Add(new NetworkTopology());
        using var stream = new ThrowingWritableStream();

        var act = () => _writer.WriteStream(project, stream);

        var ex = act.Should().Throw<CpjWriteException>().Which;
        ex.Message.Should().Contain("Failed to write CPJ content to stream.");
        ex.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task WriteStreamAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        var project = new NodelistProject();
        project.Networks.Add(new NetworkTopology());
        using var stream = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _writer.WriteStreamAsync(project, stream, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WriteStreamAsync_GenerationThrowsCpjWriteException_Rethrows()
    {
        var project = new NodelistProject();
        project.Networks.Add(null!);
        using var stream = new MemoryStream();

        var act = () => _writer.WriteStreamAsync(project, stream);

        var ex = (await act.Should().ThrowAsync<CpjWriteException>()).Which;
        ex.SectionName.Should().Be("Topology");
    }

    [Fact]
    public async Task WriteStreamAsync_StreamWriteThrows_WrapsInCpjWriteException()
    {
        var project = new NodelistProject();
        project.Networks.Add(new NetworkTopology());
        using var stream = new ThrowingWritableStream();

        var act = () => _writer.WriteStreamAsync(project, stream);

        var ex = (await act.Should().ThrowAsync<CpjWriteException>()).Which;
        ex.Message.Should().Contain("Failed to write CPJ content to stream.");
        ex.InnerException.Should().BeOfType<InvalidOperationException>();
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
