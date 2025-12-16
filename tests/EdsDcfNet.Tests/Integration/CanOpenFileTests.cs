namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Models;
using FluentAssertions;
using Xunit;

public class CanOpenFileTests
{
    #region ReadEds Tests

    [Fact]
    public void ReadEds_ValidFile_ReturnsElectronicDataSheet()
    {
        // Arrange
        var filePath = "Fixtures/sample_device.eds";

        // Act
        var result = CanOpenFile.ReadEds(filePath);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ElectronicDataSheet>();
        result.FileInfo.FileName.Should().Be("sample_device.eds");
        result.DeviceInfo.ProductName.Should().Be("IO-Module 16x16");
    }

    [Fact]
    public void ReadEds_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var filePath = "NonExistent.eds";

        // Act
        var act = () => CanOpenFile.ReadEds(filePath);

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    #endregion

    #region ReadEdsFromString Tests

    [Fact]
    public void ReadEdsFromString_ValidContent_ReturnsElectronicDataSheet()
    {
        // Arrange
        var content = @"
[FileInfo]
FileName=test.eds
FileVersion=1
FileRevision=0

[DeviceInfo]
VendorName=Test Vendor
ProductName=Test Product
VendorNumber=0x100

[DummyUsage]
Dummy0002=1

[MandatoryObjects]
SupportedObjects=1
1=0x1000

[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x191
PDOMapping=0
";

        // Act
        var result = CanOpenFile.ReadEdsFromString(content);

        // Assert
        result.Should().NotBeNull();
        result.FileInfo.FileName.Should().Be("test.eds");
        result.DeviceInfo.VendorName.Should().Be("Test Vendor");
        result.DeviceInfo.ProductName.Should().Be("Test Product");
    }

    #endregion

    #region ReadDcfFromString Tests

    [Fact]
    public void ReadDcfFromString_ValidContent_ReturnsDeviceConfigurationFile()
    {
        // Arrange
        var content = @"
[FileInfo]
FileName=test.dcf
FileVersion=1
FileRevision=0

[DeviceInfo]
VendorName=Test Vendor
ProductName=Test Product

[DeviceCommissioning]
NodeID=5
NodeName=TestNode
Baudrate=500

[DummyUsage]
Dummy0002=1

[MandatoryObjects]
SupportedObjects=1
1=0x1000

[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x191
PDOMapping=0
";

        // Act
        var result = CanOpenFile.ReadDcfFromString(content);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<DeviceConfigurationFile>();
        result.DeviceCommissioning.NodeId.Should().Be(5);
        result.DeviceCommissioning.NodeName.Should().Be("TestNode");
        result.DeviceCommissioning.Baudrate.Should().Be(500);
    }

    #endregion

    #region WriteDcfToString Tests

    [Fact]
    public void WriteDcfToString_ValidDcf_GeneratesString()
    {
        // Arrange
        var dcf = new DeviceConfigurationFile
        {
            FileInfo = new EdsFileInfo
            {
                FileName = "test.dcf",
                FileVersion = 1,
                FileRevision = 0,
                EdsVersion = "4.0"
            },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Test Vendor",
                ProductName = "Test Product",
                VendorNumber = 0x100,
                ProductNumber = 0x1001
            },
            DeviceCommissioning = new DeviceCommissioning
            {
                NodeId = 5,
                Baudrate = 500,
                NodeName = "TestNode"
            },
            ObjectDictionary = new ObjectDictionary()
        };

        dcf.ObjectDictionary.MandatoryObjects.Add(0x1000);
        dcf.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x191",
            PdoMapping = false
        };

        // Act
        var result = CanOpenFile.WriteDcfToString(dcf);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("[FileInfo]");
        result.Should().Contain("[DeviceInfo]");
        result.Should().Contain("[DeviceCommissioning]");
        result.Should().Contain("NodeID=5");
        result.Should().Contain("Baudrate=500");
    }

    #endregion

    #region EdsToDcf Tests

    [Fact]
    public void EdsToDcf_ValidEds_CreatesDcfWithCommissioning()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo
            {
                FileName = "test.eds",
                FileVersion = 1,
                FileRevision = 0,
                EdsVersion = "4.0"
            },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Test Vendor",
                ProductName = "Test Product",
                VendorNumber = 0x100
            },
            ObjectDictionary = new ObjectDictionary()
        };

        // Act
        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5, baudrate: 500, nodeName: "MyDevice");

        // Assert
        dcf.Should().NotBeNull();
        dcf.Should().BeOfType<DeviceConfigurationFile>();
        dcf.DeviceCommissioning.NodeId.Should().Be(5);
        dcf.DeviceCommissioning.Baudrate.Should().Be(500);
        dcf.DeviceCommissioning.NodeName.Should().Be("MyDevice");
        dcf.FileInfo.FileName.Should().Be("test.dcf");
    }

    [Fact]
    public void EdsToDcf_DefaultBaudrate_Uses250()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test Product" },
            ObjectDictionary = new ObjectDictionary()
        };

        // Act
        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Assert
        dcf.DeviceCommissioning.Baudrate.Should().Be(250);
    }

    [Fact]
    public void EdsToDcf_NoNodeName_GeneratesDefault()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test Product" },
            ObjectDictionary = new ObjectDictionary()
        };

        // Act
        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Assert
        dcf.DeviceCommissioning.NodeName.Should().Be("Test Product_Node5");
    }

    [Fact]
    public void EdsToDcf_IncrementsFileRevision()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo
            {
                FileName = "test.eds",
                FileVersion = 2,
                FileRevision = 3
            },
            DeviceInfo = new DeviceInfo { ProductName = "Test" },
            ObjectDictionary = new ObjectDictionary()
        };

        // Act
        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Assert
        dcf.FileInfo.FileVersion.Should().Be(2);
        dcf.FileInfo.FileRevision.Should().Be(4); // Incremented
    }

    [Fact]
    public void EdsToDcf_PreservesDeviceInfo()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Test Vendor",
                ProductName = "Test Product",
                VendorNumber = 0x100,
                ProductNumber = 0x1001,
                OrderCode = "TEST-001"
            },
            ObjectDictionary = new ObjectDictionary()
        };

        // Act
        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Assert
        dcf.DeviceInfo.Should().BeSameAs(eds.DeviceInfo);
        dcf.DeviceInfo.VendorName.Should().Be("Test Vendor");
        dcf.DeviceInfo.ProductName.Should().Be("Test Product");
        dcf.DeviceInfo.VendorNumber.Should().Be(0x100);
    }

    [Fact]
    public void EdsToDcf_PreservesObjectDictionary()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test" },
            ObjectDictionary = new ObjectDictionary()
        };

        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type"
        };

        // Act
        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Assert
        dcf.ObjectDictionary.Should().BeSameAs(eds.ObjectDictionary);
        dcf.ObjectDictionary.Objects.Should().ContainKey(0x1000);
    }

    [Fact]
    public void EdsToDcf_SetsLastEds()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "original.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test" },
            ObjectDictionary = new ObjectDictionary()
        };

        // Act
        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Assert
        dcf.FileInfo.LastEds.Should().Be("original.eds");
    }

    #endregion

    #region Round-trip Tests

    [Fact]
    public void RoundTrip_EdsToString_PreservesData()
    {
        // Arrange
        var filePath = "Fixtures/sample_device.eds";
        var originalEds = CanOpenFile.ReadEds(filePath);

        // Convert to DCF
        var dcf = CanOpenFile.EdsToDcf(originalEds, nodeId: 5, baudrate: 500);

        // Write to string
        var dcfString = CanOpenFile.WriteDcfToString(dcf);

        // Act - Read back from string
        var parsedDcf = CanOpenFile.ReadDcfFromString(dcfString);

        // Assert
        parsedDcf.DeviceInfo.VendorName.Should().Be(originalEds.DeviceInfo.VendorName);
        parsedDcf.DeviceInfo.ProductName.Should().Be(originalEds.DeviceInfo.ProductName);
        parsedDcf.DeviceCommissioning.NodeId.Should().Be(5);
        parsedDcf.DeviceCommissioning.Baudrate.Should().Be(500);
        parsedDcf.ObjectDictionary.MandatoryObjects.Should().BeEquivalentTo(originalEds.ObjectDictionary.MandatoryObjects);
    }

    [Fact]
    public void RoundTrip_EdsToDcfWriteRead_PreservesObjects()
    {
        // Arrange
        var filePath = "Fixtures/sample_device.eds";
        var originalEds = CanOpenFile.ReadEds(filePath);

        // Act
        var dcf = CanOpenFile.EdsToDcf(originalEds, nodeId: 10, baudrate: 250);
        var dcfString = CanOpenFile.WriteDcfToString(dcf);
        var parsedDcf = CanOpenFile.ReadDcfFromString(dcfString);

        // Assert - Check specific objects are preserved
        parsedDcf.ObjectDictionary.Objects.Should().ContainKey(0x1000);
        parsedDcf.ObjectDictionary.Objects[0x1000].ParameterName.Should().Be("Device Type");
        parsedDcf.ObjectDictionary.Objects[0x1000].DefaultValue.Should().Be(originalEds.ObjectDictionary.Objects[0x1000].DefaultValue);
    }

    #endregion
}
