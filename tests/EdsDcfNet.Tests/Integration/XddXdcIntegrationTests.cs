namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Writers;

public class XddXdcIntegrationTests
{
    private readonly XddReader _xddReader = new();
    private readonly XdcReader _xdcReader = new();
    private readonly XddWriter _xddWriter = new();
    private readonly XdcWriter _xdcWriter = new();
    private readonly EdsReader _edsReader = new();
    private readonly DcfWriter _dcfWriter = new();

    #region XDD Round-Trip Tests

    [Fact]
    public void ReadXdd_WriteXddToString_ReadXddFromString_PreservesSemantics()
    {
        // Arrange — read sample XDD
        var original = _xddReader.ReadFile("Fixtures/sample_device.xdd");

        // Act — write as XDD string, then read back
        var xddContent = _xddWriter.GenerateString(original);
        var roundTripped = _xddReader.ReadString(xddContent);

        // Assert — key fields must survive the round-trip
        roundTripped.DeviceInfo.VendorName.Should().Be(original.DeviceInfo.VendorName);
        roundTripped.DeviceInfo.ProductName.Should().Be(original.DeviceInfo.ProductName);
        roundTripped.DeviceInfo.VendorNumber.Should().Be(original.DeviceInfo.VendorNumber);
        roundTripped.DeviceInfo.ProductNumber.Should().Be(original.DeviceInfo.ProductNumber);
        AssertObjectDictionaryKeysAndNamesEqual(original.ObjectDictionary, roundTripped.ObjectDictionary);
    }

    [Fact]
    public void CanOpenFile_ReadXdd_WriteXdd_ReadXdd_RoundTripPreservesSemantics()
    {
        // Arrange
        var original = CanOpenFile.ReadXdd("Fixtures/sample_device.xdd");

        // Act
        var written = CanOpenFile.WriteXddToString(original);
        var roundTripped = CanOpenFile.ReadXddFromString(written);

        // Assert
        roundTripped.DeviceInfo.VendorName.Should().Be(original.DeviceInfo.VendorName);
        roundTripped.DeviceInfo.ProductName.Should().Be(original.DeviceInfo.ProductName);
        roundTripped.DeviceInfo.VendorNumber.Should().Be(original.DeviceInfo.VendorNumber);
        roundTripped.DeviceInfo.ProductNumber.Should().Be(original.DeviceInfo.ProductNumber);
        roundTripped.ObjectDictionary.Objects.Keys.Should().BeEquivalentTo(original.ObjectDictionary.Objects.Keys);

        foreach (var index in original.ObjectDictionary.Objects.Keys)
        {
            roundTripped.ObjectDictionary.Objects[index].ObjectType
                .Should().Be(original.ObjectDictionary.Objects[index].ObjectType);
            roundTripped.ObjectDictionary.Objects[index].ParameterName
                .Should().Be(original.ObjectDictionary.Objects[index].ParameterName);
        }
    }

    #endregion

    #region XDC Round-Trip Tests

    [Fact]
    public void ReadXdc_WriteXdcToString_ReadXdcFromString_PreservesSemantics()
    {
        // Arrange
        var original = _xdcReader.ReadFile("Fixtures/minimal.xdc");

        // Act
        var xdcContent = _xdcWriter.GenerateString(original);
        var roundTripped = _xdcReader.ReadString(xdcContent);

        // Assert — DeviceCommissioning
        roundTripped.DeviceCommissioning.NodeId.Should().Be(original.DeviceCommissioning.NodeId);
        roundTripped.DeviceCommissioning.Baudrate.Should().Be(original.DeviceCommissioning.Baudrate);
        roundTripped.DeviceCommissioning.NodeName.Should().Be(original.DeviceCommissioning.NodeName);
        AssertObjectDictionaryKeysAndNamesEqual(original.ObjectDictionary, roundTripped.ObjectDictionary);
    }

    [Fact]
    public void CanOpenFile_ReadXdc_WriteXdc_ReadXdc_RoundTripPreservesSemantics()
    {
        // Arrange
        var original = CanOpenFile.ReadXdc("Fixtures/minimal.xdc");

        // Act
        var written = CanOpenFile.WriteXdcToString(original);
        var roundTripped = CanOpenFile.ReadXdcFromString(written);

        // Assert
        roundTripped.DeviceCommissioning.NodeId.Should().Be(original.DeviceCommissioning.NodeId);
        roundTripped.DeviceCommissioning.Baudrate.Should().Be(original.DeviceCommissioning.Baudrate);
        roundTripped.DeviceCommissioning.NodeName.Should().Be(original.DeviceCommissioning.NodeName);
        roundTripped.ObjectDictionary.Objects.Keys.Should().BeEquivalentTo(original.ObjectDictionary.Objects.Keys);
    }

    #endregion

    #region Cross-Format Conversion Tests

    [Fact]
    public void ReadEds_WriteXdd_ReadXdd_ObjectDictionaryMatches()
    {
        // Arrange — read EDS
        var eds = _edsReader.ReadFile("Fixtures/sample_device.eds");

        // Act — write EDS model as XDD, read back
        var xddContent = _xddWriter.GenerateString(eds);
        var fromXdd = _xddReader.ReadString(xddContent);

        // Assert — object counts and key objects must match
        fromXdd.ObjectDictionary.Objects.Count.Should().Be(eds.ObjectDictionary.Objects.Count);

        foreach (var idx in eds.ObjectDictionary.Objects.Keys)
        {
            fromXdd.ObjectDictionary.Objects.Should().ContainKey(idx);
            fromXdd.ObjectDictionary.Objects[idx].ParameterName
                .Should().Be(eds.ObjectDictionary.Objects[idx].ParameterName);
            fromXdd.ObjectDictionary.Objects[idx].ObjectType
                .Should().Be(eds.ObjectDictionary.Objects[idx].ObjectType);
        }
    }

    [Fact]
    public void ReadXdd_ConvertToDcf_WriteDcfToString_ReadDcfFromString_ObjectDictionaryMatches()
    {
        // Arrange — read XDD
        var fromXdd = _xddReader.ReadFile("Fixtures/sample_device.xdd");

        // Act — convert to DCF (using EdsToDcf path), write, read back
        var dcf = CanOpenFile.EdsToDcf(fromXdd, nodeId: 5, baudrate: 500, nodeName: "TestNode");
        var dcfContent = _dcfWriter.GenerateString(dcf);
        var fromDcf = new DcfReader().ReadString(dcfContent);

        // Assert — object count must match
        fromDcf.ObjectDictionary.Objects.Count.Should().Be(fromXdd.ObjectDictionary.Objects.Count);

        // Assert — check a specific object
        fromDcf.ObjectDictionary.Objects.Should().ContainKey(0x1000);
        fromDcf.ObjectDictionary.Objects[0x1000].ParameterName.Should().Be("Device Type");
    }

    [Fact]
    public void ReadXdc_WriteDcf_DeviceCommissioningMatches()
    {
        // Arrange
        var xdc = _xdcReader.ReadFile("Fixtures/minimal.xdc");

        // Act — write as DCF, read back
        var dcfContent = _dcfWriter.GenerateString(xdc);
        var fromDcf = new DcfReader().ReadString(dcfContent);

        // Assert
        fromDcf.DeviceCommissioning.NodeId.Should().Be(xdc.DeviceCommissioning.NodeId);
        fromDcf.DeviceCommissioning.Baudrate.Should().Be(xdc.DeviceCommissioning.Baudrate);
    }

    private static void AssertObjectDictionaryKeysAndNamesEqual(ObjectDictionary expected, ObjectDictionary actual)
    {
        actual.Objects.Count.Should().Be(expected.Objects.Count);

        foreach (var idx in expected.Objects.Keys)
        {
            actual.Objects.Should().ContainKey(idx);
            actual.Objects[idx].ParameterName.Should().Be(expected.Objects[idx].ParameterName);
        }
    }

    #endregion
}
