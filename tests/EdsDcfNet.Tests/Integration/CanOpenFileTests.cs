namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Validation;
using FluentAssertions;
using Xunit;

public class CanOpenFileTests
{
    [Fact]
    public void SyncReadMethods_LegacySingleParameterOverloads_AreAvailableForBinaryCompatibility()
    {
        var singleStringSignature = new[] { typeof(string) };

        typeof(CanOpenFile).GetMethod(nameof(CanOpenFile.ReadEds), singleStringSignature).Should().NotBeNull();
        typeof(CanOpenFile).GetMethod(nameof(CanOpenFile.ReadDcf), singleStringSignature).Should().NotBeNull();
        typeof(CanOpenFile).GetMethod(nameof(CanOpenFile.ReadCpj), singleStringSignature).Should().NotBeNull();
        typeof(CanOpenFile).GetMethod(nameof(CanOpenFile.ReadXdd), singleStringSignature).Should().NotBeNull();
        typeof(CanOpenFile).GetMethod(nameof(CanOpenFile.ReadXdc), singleStringSignature).Should().NotBeNull();
    }

    [Fact]
    public void SyncReadMethods_DefaultAndExplicitMaxInputSizeOverloads_BothWork()
    {
        var edsDefault = CanOpenFile.ReadEds("Fixtures/sample_device.eds");
        var edsExplicit = CanOpenFile.ReadEds("Fixtures/sample_device.eds", IniParser.DefaultMaxInputSize);
        edsDefault.FileInfo.FileName.Should().Be(edsExplicit.FileInfo.FileName);

        var dcfDefault = CanOpenFile.ReadDcf("Fixtures/minimal.dcf");
        var dcfExplicit = CanOpenFile.ReadDcf("Fixtures/minimal.dcf", IniParser.DefaultMaxInputSize);
        dcfDefault.DeviceCommissioning.NodeId.Should().Be(dcfExplicit.DeviceCommissioning.NodeId);

        var xddDefault = CanOpenFile.ReadXdd("Fixtures/sample_device.xdd");
        var xddExplicit = CanOpenFile.ReadXdd("Fixtures/sample_device.xdd", IniParser.DefaultMaxInputSize);
        xddDefault.DeviceInfo.ProductName.Should().Be(xddExplicit.DeviceInfo.ProductName);

        var xdcDefault = CanOpenFile.ReadXdc("Fixtures/minimal.xdc");
        var xdcExplicit = CanOpenFile.ReadXdc("Fixtures/minimal.xdc", IniParser.DefaultMaxInputSize);
        xdcDefault.DeviceCommissioning.NodeId.Should().Be(xdcExplicit.DeviceCommissioning.NodeId);

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "[Topology]\nNetName=Coverage Network\nNodes=0");

            var cpjDefault = CanOpenFile.ReadCpj(tempFile);
            var cpjExplicit = CanOpenFile.ReadCpj(tempFile, IniParser.DefaultMaxInputSize);

            cpjDefault.Networks.Should().ContainSingle();
            cpjDefault.Networks[0].NetName.Should().Be("Coverage Network");
            cpjExplicit.Networks.Should().ContainSingle();
            cpjExplicit.Networks[0].NetName.Should().Be("Coverage Network");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void SyncReadFromStringMethods_LegacySingleParameterOverloads_AreAvailableForBinaryCompatibility()
    {
        var singleStringSignature = new[] { typeof(string) };

        typeof(CanOpenFile).GetMethod(nameof(CanOpenFile.ReadEdsFromString), singleStringSignature).Should().NotBeNull();
        typeof(CanOpenFile).GetMethod(nameof(CanOpenFile.ReadDcfFromString), singleStringSignature).Should().NotBeNull();
        typeof(CanOpenFile).GetMethod(nameof(CanOpenFile.ReadCpjFromString), singleStringSignature).Should().NotBeNull();
        typeof(CanOpenFile).GetMethod(nameof(CanOpenFile.ReadXddFromString), singleStringSignature).Should().NotBeNull();
        typeof(CanOpenFile).GetMethod(nameof(CanOpenFile.ReadXdcFromString), singleStringSignature).Should().NotBeNull();
    }

    [Fact]
    public void SyncReadFromStringMethods_DefaultAndExplicitMaxInputSizeOverloads_BothWork()
    {
        const string edsContent = """
                                  [FileInfo]
                                  FileName=fromstring.eds
                                  FileVersion=1
                                  [DeviceInfo]
                                  VendorName=Vendor
                                  ProductName=Product
                                  [MandatoryObjects]
                                  SupportedObjects=1
                                  1=0x1000
                                  [1000]
                                  ParameterName=Device Type
                                  ObjectType=0x7
                                  DataType=0x0007
                                  AccessType=ro
                                  PDOMapping=0
                                  """;
        var edsDefault = CanOpenFile.ReadEdsFromString(edsContent);
        var edsExplicit = CanOpenFile.ReadEdsFromString(edsContent, IniParser.DefaultMaxInputSize);
        edsDefault.FileInfo.FileName.Should().Be(edsExplicit.FileInfo.FileName);

        const string dcfContent = """
                                  [FileInfo]
                                  FileName=fromstring.dcf
                                  FileVersion=1
                                  [DeviceInfo]
                                  VendorName=Vendor
                                  ProductName=Product
                                  [DeviceCommissioning]
                                  NodeID=5
                                  Baudrate=500
                                  [MandatoryObjects]
                                  SupportedObjects=1
                                  1=0x1000
                                  [1000]
                                  ParameterName=Device Type
                                  ObjectType=0x7
                                  DataType=0x0007
                                  AccessType=ro
                                  PDOMapping=0
                                  """;
        var dcfDefault = CanOpenFile.ReadDcfFromString(dcfContent);
        var dcfExplicit = CanOpenFile.ReadDcfFromString(dcfContent, IniParser.DefaultMaxInputSize);
        dcfDefault.DeviceCommissioning.NodeId.Should().Be(dcfExplicit.DeviceCommissioning.NodeId);

        const string cpjContent = "[Topology]\nNetName=FromString\nNodes=0";
        var cpjDefault = CanOpenFile.ReadCpjFromString(cpjContent);
        var cpjExplicit = CanOpenFile.ReadCpjFromString(cpjContent, IniParser.DefaultMaxInputSize);
        cpjDefault.Networks[0].NetName.Should().Be(cpjExplicit.Networks[0].NetName);

        var xddContent = File.ReadAllText("Fixtures/sample_device.xdd");
        var xddDefault = CanOpenFile.ReadXddFromString(xddContent);
        var xddExplicit = CanOpenFile.ReadXddFromString(xddContent, IniParser.DefaultMaxInputSize);
        xddDefault.DeviceInfo.ProductName.Should().Be(xddExplicit.DeviceInfo.ProductName);

        var xdcContent = File.ReadAllText("Fixtures/minimal.xdc");
        var xdcDefault = CanOpenFile.ReadXdcFromString(xdcContent);
        var xdcExplicit = CanOpenFile.ReadXdcFromString(xdcContent, IniParser.DefaultMaxInputSize);
        xdcDefault.DeviceCommissioning.NodeId.Should().Be(xdcExplicit.DeviceCommissioning.NodeId);
    }

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

    [Fact]
    public void ReadEds_WithExplicitMaxInputSize_InvokesOverload()
    {
        var result = CanOpenFile.ReadEds("Fixtures/sample_device.eds", IniParser.DefaultMaxInputSize);

        result.Should().NotBeNull();
        result.FileInfo.FileName.Should().Be("sample_device.eds");
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

    [Fact]
    public void ReadEdsFromString_WithExplicitMaxInputSize_InvokesOverload()
    {
        var content = "[FileInfo]\nFileName=size.eds\nFileVersion=1\n[DeviceInfo]\nVendorName=V\n[MandatoryObjects]\nSupportedObjects=1\n1=0x1000\n[1000]\nParameterName=Device Type\nObjectType=0x7\nDataType=0x0007\nAccessType=ro\nPDOMapping=0\n";

        var result = CanOpenFile.ReadEdsFromString(content, IniParser.DefaultMaxInputSize);

        result.Should().NotBeNull();
        result.FileInfo.FileName.Should().Be("size.eds");
    }

    #endregion

    #region WriteEds Tests

    [Fact]
    public void WriteEdsToString_ValidEds_GeneratesString()
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
                VendorNumber = 0x100,
                ProductNumber = 0x1001
            },
            ObjectDictionary = new ObjectDictionary()
        };

        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x191",
            PdoMapping = false,
            ParameterValue = "0x999"
        };

        // Act
        var result = CanOpenFile.WriteEdsToString(eds);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("[FileInfo]");
        result.Should().Contain("[DeviceInfo]");
        result.Should().Contain("[MandatoryObjects]");
        result.Should().Contain("[1000]");
        result.Should().NotContain("[DeviceCommissioning]");
        result.Should().NotContain("ParameterValue=");
    }

    [Fact]
    public void WriteEds_ValidEds_WritesAndReadsBackFile()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "roundtrip.eds" },
            DeviceInfo = new DeviceInfo { VendorName = "Test Vendor", ProductName = "Test Product" },
            ObjectDictionary = new ObjectDictionary()
        };

        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x191",
            PdoMapping = false
        };

        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            CanOpenFile.WriteEds(eds, tempFile);
            var result = CanOpenFile.ReadEds(tempFile);

            // Assert
            result.FileInfo.FileName.Should().Be("roundtrip.eds");
            result.DeviceInfo.VendorName.Should().Be("Test Vendor");
            result.ObjectDictionary.Objects.Should().ContainKey(0x1000);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
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

    [Fact]
    public void ReadDcfFromString_WithExplicitMaxInputSize_InvokesOverload()
    {
        var content = "[FileInfo]\nFileName=size.dcf\nFileVersion=1\n[DeviceInfo]\nVendorName=V\n[DeviceCommissioning]\nNodeID=1\nBaudrate=250\n[MandatoryObjects]\nSupportedObjects=1\n1=0x1000\n[1000]\nParameterName=Device Type\nObjectType=0x7\nDataType=0x0007\nAccessType=ro\nPDOMapping=0\n";

        var result = CanOpenFile.ReadDcfFromString(content, IniParser.DefaultMaxInputSize);

        result.Should().NotBeNull();
        result.FileInfo.FileName.Should().Be("size.dcf");
    }

    #endregion

    #region WriteDcf/ReadDcf Tests

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

    [Fact]
    public void WriteDcf_ValidDcf_WritesAndReadsBackFile()
    {
        // Arrange
        var dcf = new DeviceConfigurationFile
        {
            FileInfo = new EdsFileInfo { FileName = "roundtrip.dcf" },
            DeviceInfo = new DeviceInfo { VendorName = "Test Vendor", ProductName = "Test Product" },
            DeviceCommissioning = new DeviceCommissioning { NodeId = 3, Baudrate = 250 },
            ObjectDictionary = new ObjectDictionary()
        };
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            CanOpenFile.WriteDcf(dcf, tempFile);
            var result = CanOpenFile.ReadDcf(tempFile);

            // Assert
            result.DeviceCommissioning.NodeId.Should().Be(3);
            result.DeviceCommissioning.Baudrate.Should().Be(250);
            result.DeviceInfo.VendorName.Should().Be("Test Vendor");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void ReadDcf_WithExplicitMaxInputSize_InvokesOverload()
    {
        var result = CanOpenFile.ReadDcf("Fixtures/minimal.dcf", IniParser.DefaultMaxInputSize);

        result.Should().NotBeNull();
        result.DeviceCommissioning.NodeId.Should().Be(5);
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
    public void EdsToDcf_NodeIdZero_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test Product" },
            ObjectDictionary = new ObjectDictionary()
        };

        // Act
        var act = () => CanOpenFile.EdsToDcf(eds, nodeId: 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Node-ID must be in range 1..127*");
    }

    [Fact]
    public void EdsToDcf_NodeIdAbove127_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test Product" },
            ObjectDictionary = new ObjectDictionary()
        };

        // Act
        var act = () => CanOpenFile.EdsToDcf(eds, nodeId: 200);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Node-ID must be in range 1..127*");
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
    public void EdsToDcf_WithExplicitTimestamp_UsesProvidedFileInfoTimeFields()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test Product" },
            ObjectDictionary = new ObjectDictionary()
        };
        var timestamp = new DateTime(2026, 03, 05, 13, 47, 00);

        // Act
        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5, timestamp: timestamp);

        // Assert
        dcf.FileInfo.CreationDate.Should().Be("03-05-2026");
        dcf.FileInfo.CreationTime.Should().Be("01:47PM");
    }

    [Fact]
    public void EdsToDcf_DefaultOverload_UsesSpecCompliantDateAndTimeFormatting()
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
        DateTime.TryParseExact(
            dcf.FileInfo.CreationDate,
            "MM-dd-yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out _).Should().BeTrue();
        DateTime.TryParseExact(
            dcf.FileInfo.CreationTime,
            "hh:mmtt",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out _).Should().BeTrue();
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

        // Assert - deep copy: not the same reference, but equal values
        dcf.DeviceInfo.Should().NotBeSameAs(eds.DeviceInfo);
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

        // Assert - deep copy: not the same reference, but equal values
        dcf.ObjectDictionary.Should().NotBeSameAs(eds.ObjectDictionary);
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

    #region EdsToDcf Mutation Isolation Tests

    [Fact]
    public void EdsToDcf_MutatingDcfObjectDictionary_DoesNotAffectEds()
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
            ParameterName = "Device Type",
            DefaultValue = "0x191"
        };

        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Act - mutate the DCF
        dcf.ObjectDictionary.Objects[0x1000].ParameterValue = "0x999";
        dcf.ObjectDictionary.Objects[0x1000].ParameterName = "Modified";

        // Assert - EDS must be unchanged
        eds.ObjectDictionary.Objects[0x1000].ParameterValue.Should().BeNull();
        eds.ObjectDictionary.Objects[0x1000].ParameterName.Should().Be("Device Type");
    }

    [Fact]
    public void EdsToDcf_TwoDcfsFromSameEds_AreMutuallyIsolated()
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
            ParameterName = "Device Type",
            DefaultValue = "0x191"
        };

        var dcf1 = CanOpenFile.EdsToDcf(eds, nodeId: 1);
        var dcf2 = CanOpenFile.EdsToDcf(eds, nodeId: 2);

        // Act - mutate dcf1
        dcf1.ObjectDictionary.Objects[0x1000].ParameterValue = "0xAAA";

        // Assert - dcf2 must be unaffected
        dcf2.ObjectDictionary.Objects[0x1000].ParameterValue.Should().BeNull();
    }

    [Fact]
    public void EdsToDcf_MutatingDcfDeviceInfo_DoesNotAffectEds()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Original Vendor",
                ProductName = "Original Product"
            },
            ObjectDictionary = new ObjectDictionary()
        };

        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Act - mutate the DCF's DeviceInfo
        dcf.DeviceInfo.VendorName = "Modified Vendor";

        // Assert - EDS must be unchanged
        eds.DeviceInfo.VendorName.Should().Be("Original Vendor");
    }

    [Fact]
    public void EdsToDcf_MutatingDcfSubObjects_DoesNotAffectEds()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test" },
            ObjectDictionary = new ObjectDictionary()
        };

        eds.ObjectDictionary.MandatoryObjects.Add(0x1018);
        var obj = new CanOpenObject
        {
            Index = 0x1018,
            ParameterName = "Identity",
            SubNumber = 4
        };
        obj.SubObjects[0] = new CanOpenSubObject
        {
            SubIndex = 0,
            ParameterName = "Number of Entries",
            DefaultValue = "4"
        };
        obj.SubObjects[1] = new CanOpenSubObject
        {
            SubIndex = 1,
            ParameterName = "Vendor ID",
            DefaultValue = "0x100"
        };
        eds.ObjectDictionary.Objects[0x1018] = obj;

        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Act - mutate a sub-object in the DCF
        dcf.ObjectDictionary.Objects[0x1018].SubObjects[1].ParameterValue = "0x200";

        // Assert - EDS sub-object must be unchanged
        eds.ObjectDictionary.Objects[0x1018].SubObjects[1].ParameterValue.Should().BeNull();
    }

    [Fact]
    public void EdsToDcf_MutatingDcfComments_DoesNotAffectEds()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test" },
            ObjectDictionary = new ObjectDictionary(),
            Comments = new Comments { Lines = 1 }
        };
        eds.Comments.CommentLines[1] = "Original comment";

        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Act - mutate the DCF's comments
        dcf.Comments!.CommentLines[1] = "Modified comment";

        // Assert - EDS comments must be unchanged
        eds.Comments!.CommentLines[1].Should().Be("Original comment");
    }

    #endregion

    #region EdsToDcf SupportedModules Tests

    [Fact]
    public void EdsToDcf_WithSupportedModules_ClonesModulesList()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test" },
            ObjectDictionary = new ObjectDictionary()
        };

        eds.SupportedModules.Add(new ModuleInfo
        {
            ModuleNumber = 1,
            ProductName = "Input Module",
            ProductVersion = 1,
            ProductRevision = 0,
            OrderCode = "MOD-IN-8"
        });

        // Act
        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Assert
        dcf.SupportedModules.Should().HaveCount(1);
        dcf.SupportedModules[0].ProductName.Should().Be("Input Module");
        dcf.SupportedModules.Should().NotBeSameAs(eds.SupportedModules);
    }

    [Fact]
    public void EdsToDcf_WithModuleFixedObjectDefinitions_ClonesCorrectly()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test" },
            ObjectDictionary = new ObjectDictionary()
        };

        var module = new ModuleInfo { ModuleNumber = 1, ProductName = "Module A" };
        module.FixedObjectDefinitions[0x6000] = new CanOpenObject
        {
            Index = 0x6000,
            ParameterName = "Digital Input",
            ObjectType = 0x8,
            DataType = 0x0005
        };
        eds.SupportedModules.Add(module);

        // Act
        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Assert
        dcf.SupportedModules[0].FixedObjectDefinitions.Should().ContainKey(0x6000);
        dcf.SupportedModules[0].FixedObjectDefinitions[0x6000].ParameterName.Should().Be("Digital Input");
        dcf.SupportedModules[0].FixedObjectDefinitions.Should().NotBeSameAs(
            eds.SupportedModules[0].FixedObjectDefinitions);
    }

    [Fact]
    public void EdsToDcf_WithModuleSubExtensionDefinitions_ClonesCorrectly()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test" },
            ObjectDictionary = new ObjectDictionary()
        };

        var module = new ModuleInfo { ModuleNumber = 1, ProductName = "Module B" };
        module.SubExtensionDefinitions[0x6100] = new ModuleSubExtension
        {
            Index = 0x6100,
            ParameterName = "Digital Output",
            DataType = 0x0005,
            AccessType = AccessType.ReadWrite,
            DefaultValue = "0",
            PdoMapping = true,
            Count = "8",
            ObjExtend = 0
        };
        eds.SupportedModules.Add(module);

        // Act
        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Assert
        dcf.SupportedModules[0].SubExtensionDefinitions.Should().ContainKey(0x6100);
        var ext = dcf.SupportedModules[0].SubExtensionDefinitions[0x6100];
        ext.ParameterName.Should().Be("Digital Output");
        ext.DataType.Should().Be(0x0005);
        ext.AccessType.Should().Be(AccessType.ReadWrite);
        ext.DefaultValue.Should().Be("0");
        ext.PdoMapping.Should().BeTrue();
        ext.Count.Should().Be("8");
        ext.ObjExtend.Should().Be(0);
        dcf.SupportedModules[0].SubExtensionDefinitions.Should().NotBeSameAs(
            eds.SupportedModules[0].SubExtensionDefinitions);
    }

    [Fact]
    public void EdsToDcf_MutatingDcfSupportedModules_DoesNotAffectEds()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test" },
            ObjectDictionary = new ObjectDictionary()
        };

        var module = new ModuleInfo { ModuleNumber = 1, ProductName = "Original Module" };
        module.FixedObjectDefinitions[0x6000] = new CanOpenObject
        {
            Index = 0x6000,
            ParameterName = "Original Name"
        };
        eds.SupportedModules.Add(module);

        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Act - mutate the DCF module
        dcf.SupportedModules[0].ProductName = "Modified Module";
        dcf.SupportedModules[0].FixedObjectDefinitions[0x6000].ParameterName = "Modified Name";

        // Assert - EDS must be unchanged
        eds.SupportedModules[0].ProductName.Should().Be("Original Module");
        eds.SupportedModules[0].FixedObjectDefinitions[0x6000].ParameterName.Should().Be("Original Name");
    }

    [Fact]
    public void EdsToDcf_WithModuleComments_ClonesCorrectly()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test" },
            ObjectDictionary = new ObjectDictionary()
        };

        var module = new ModuleInfo
        {
            ModuleNumber = 1,
            ProductName = "Module With Comments",
            Comments = new Comments { Lines = 1 }
        };
        module.Comments!.CommentLines[1] = "Module comment";
        eds.SupportedModules.Add(module);

        // Act
        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Assert
        dcf.SupportedModules[0].Comments.Should().NotBeNull();
        dcf.SupportedModules[0].Comments!.CommentLines[1].Should().Be("Module comment");
        dcf.SupportedModules[0].Comments.Should().NotBeSameAs(eds.SupportedModules[0].Comments);
    }

    #endregion

    #region EdsToDcf DynamicChannels/Tools/ApplicationProcess Tests

    [Fact]
    public void EdsToDcf_WithDynamicChannels_ClonesCorrectly()
    {
        // Arrange
        var dynamicChannels = new DynamicChannels();
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test" },
            ObjectDictionary = new ObjectDictionary(),
            DynamicChannels = dynamicChannels
        };
        dynamicChannels.Segments.Add(new DynamicChannelSegment
        {
            Type = 0x0007,
            Dir = AccessType.ReadOnly,
            Range = "0xA000-0xA0FF",
            PPOffset = 16
        });

        // Act
        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Assert
        dcf.DynamicChannels.Should().NotBeNull();
        dcf.DynamicChannels.Should().NotBeSameAs(eds.DynamicChannels);
        var dcfDynamicChannels = dcf.DynamicChannels!;
        dcfDynamicChannels.Segments.Should().HaveCount(1);
        dcfDynamicChannels.Segments[0].Range.Should().Be("0xA000-0xA0FF");

        // Mutate DCF clone and verify source isolation
        dcfDynamicChannels.Segments[0].Range = "0xB000-0xB0FF";
        eds.DynamicChannels!.Segments[0].Range.Should().Be("0xA000-0xA0FF");
    }

    [Fact]
    public void EdsToDcf_WithTools_ClonesCorrectly()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test" },
            ObjectDictionary = new ObjectDictionary()
        };
        eds.Tools.Add(new ToolInfo { Name = "Configurator", Command = "config.exe $DCF $NODEID" });

        // Act
        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Assert
        dcf.Tools.Should().HaveCount(1);
        dcf.Tools[0].Name.Should().Be("Configurator");
        dcf.Tools[0].Should().NotBeSameAs(eds.Tools[0]);

        // Mutate DCF clone and verify source isolation
        dcf.Tools[0].Name = "Changed";
        eds.Tools[0].Name.Should().Be("Configurator");
    }

    [Fact]
    public void EdsToDcf_WithApplicationProcess_PreservesReferenceByDesign()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test" },
            ObjectDictionary = new ObjectDictionary(),
            ApplicationProcess = new ApplicationProcess()
        };
        eds.ApplicationProcess!.ParameterList.Add(new ApParameter { UniqueId = "P1", Access = "readWrite" });

        // Act
        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Assert — preservation is intentional; deep clone is deferred.
        dcf.ApplicationProcess.Should().BeSameAs(eds.ApplicationProcess);
        dcf.ApplicationProcess!.ParameterList.Should().ContainSingle(p => p.UniqueId == "P1");
    }

    #endregion

    #region EdsToDcf AdditionalSections Tests

    [Fact]
    public void EdsToDcf_WithAdditionalSections_ClonesCorrectly()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test" },
            ObjectDictionary = new ObjectDictionary()
        };
        eds.AdditionalSections["VendorSection"] = new Dictionary<string, string>
        {
            { "Key1", "Value1" },
            { "Key2", "Value2" }
        };

        // Act
        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Assert
        dcf.AdditionalSections.Should().ContainKey("VendorSection");
        dcf.AdditionalSections["VendorSection"]["Key1"].Should().Be("Value1");
        dcf.AdditionalSections["VendorSection"]["Key2"].Should().Be("Value2");
        dcf.AdditionalSections.Should().NotBeSameAs(eds.AdditionalSections);
    }

    [Fact]
    public void EdsToDcf_MutatingDcfAdditionalSections_DoesNotAffectEds()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test" },
            ObjectDictionary = new ObjectDictionary()
        };
        eds.AdditionalSections["VendorSection"] = new Dictionary<string, string>
        {
            { "Key1", "OriginalValue" }
        };

        var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Act - mutate the DCF additional sections
        dcf.AdditionalSections["VendorSection"]["Key1"] = "ModifiedValue";

        // Assert - EDS must be unchanged
        eds.AdditionalSections["VendorSection"]["Key1"].Should().Be("OriginalValue");
    }

    [Fact]
    public void EdsToDcf_AdditionalSectionInnerCaseCollision_DoesNotThrowAndUsesLastValue()
    {
        // Arrange
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.eds" },
            DeviceInfo = new DeviceInfo { ProductName = "Test" },
            ObjectDictionary = new ObjectDictionary()
        };
        eds.AdditionalSections["VendorSection"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Key"] = "First",
            ["key"] = "Second"
        };

        DeviceConfigurationFile? dcf = null;

        // Act
        var act = () => dcf = CanOpenFile.EdsToDcf(eds, nodeId: 5);

        // Assert
        act.Should().NotThrow();
        dcf.Should().NotBeNull();
        dcf!.AdditionalSections.Should().ContainKey("VendorSection");
        dcf.AdditionalSections["VendorSection"].Should().ContainKey("KEY");
        dcf.AdditionalSections["VendorSection"].Count.Should().Be(1);
        dcf.AdditionalSections["VendorSection"]["key"].Should().Be("Second");
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

    #region XDD/XDC Facade Tests

    [Fact]
    public void ReadXdd_ValidFile_ReturnsElectronicDataSheet()
    {
        var result = CanOpenFile.ReadXdd("Fixtures/sample_device.xdd");

        result.Should().NotBeNull();
        result.DeviceInfo.ProductName.Should().Be("IO-Module 16x16");
    }

    [Fact]
    public void ReadXdd_CustomMaxInputSizeTooSmall_ThrowsEdsParseException()
    {
        var act = () => CanOpenFile.ReadXdd("Fixtures/sample_device.xdd", maxInputSize: 256);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public void ReadXddFromString_ValidContent_ReturnsElectronicDataSheet()
    {
        var content = File.ReadAllText("Fixtures/sample_device.xdd");

        var result = CanOpenFile.ReadXddFromString(content);

        result.Should().NotBeNull();
        result.DeviceInfo.VendorName.Should().Be("Example Automation Inc.");
    }

    [Fact]
    public void ReadXdc_ValidFile_ReturnsDeviceConfigurationFile()
    {
        var result = CanOpenFile.ReadXdc("Fixtures/minimal.xdc");

        result.Should().NotBeNull();
        result.DeviceCommissioning.NodeId.Should().Be(5);
    }

    [Fact]
    public void ReadXdc_CustomMaxInputSizeTooSmall_ThrowsEdsParseException()
    {
        var act = () => CanOpenFile.ReadXdc("Fixtures/minimal.xdc", maxInputSize: 256);

        act.Should().Throw<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public void ReadXdcFromString_ValidContent_ReturnsDeviceConfigurationFile()
    {
        var content = File.ReadAllText("Fixtures/minimal.xdc");

        var result = CanOpenFile.ReadXdcFromString(content);

        result.Should().NotBeNull();
        result.DeviceCommissioning.Baudrate.Should().Be(500);
    }

    [Fact]
    public void WriteXddToString_ValidEds_ReturnsXddContent()
    {
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.xdd", FileVersion = 1 },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Test",
                ProductName = "Device",
                SupportedBaudRates = new BaudRates { BaudRate250 = true }
            }
        };
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000, ParameterName = "Device Type",
            ObjectType = 0x7, DataType = 0x0007, AccessType = AccessType.ReadOnly
        };
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);

        var result = CanOpenFile.WriteXddToString(eds);

        result.Should().Contain("ISO15745ProfileContainer");
        result.Should().Contain("ProfileBody_CommunicationNetwork_CANopen");
    }

    [Fact]
    public void WriteXdd_ValidEds_WritesFile()
    {
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo { FileName = "test.xdd", FileVersion = 1 },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Test",
                ProductName = "Device",
                SupportedBaudRates = new BaudRates { BaudRate250 = true }
            }
        };
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000, ParameterName = "Device Type",
            ObjectType = 0x7, DataType = 0x0007, AccessType = AccessType.ReadOnly
        };

        var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdd");
        try
        {
            CanOpenFile.WriteXdd(eds, filePath);
            File.Exists(filePath).Should().BeTrue();
            File.ReadAllText(filePath).Should().Contain("ISO15745ProfileContainer");
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void WriteXdcToString_ValidDcf_ReturnsXdcContent()
    {
        var dcf = new DeviceConfigurationFile
        {
            FileInfo = new EdsFileInfo { FileName = "test.xdc", FileVersion = 1 },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Test",
                ProductName = "Device",
                SupportedBaudRates = new BaudRates { BaudRate250 = true }
            },
            DeviceCommissioning = new DeviceCommissioning { NodeId = 3, Baudrate = 250 }
        };
        dcf.ObjectDictionary.MandatoryObjects.Add(0x1000);
        dcf.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000, ParameterName = "Device Type",
            ObjectType = 0x7, DataType = 0x0007, AccessType = AccessType.ReadOnly,
            ParameterValue = "0x00000191"
        };

        var result = CanOpenFile.WriteXdcToString(dcf);

        result.Should().Contain("deviceCommissioning");
        result.Should().Contain("actualValue");
    }

    [Fact]
    public void WriteXdc_ValidDcf_WritesFile()
    {
        var dcf = new DeviceConfigurationFile
        {
            FileInfo = new EdsFileInfo { FileName = "test.xdc", FileVersion = 1 },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Test",
                ProductName = "Device",
                SupportedBaudRates = new BaudRates { BaudRate250 = true }
            },
            DeviceCommissioning = new DeviceCommissioning { NodeId = 5, Baudrate = 500 }
        };
        dcf.ObjectDictionary.MandatoryObjects.Add(0x1000);
        dcf.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000, ParameterName = "Device Type",
            ObjectType = 0x7, DataType = 0x0007, AccessType = AccessType.ReadOnly
        };

        var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdc");
        try
        {
            CanOpenFile.WriteXdc(dcf, filePath);
            File.Exists(filePath).Should().BeTrue();
            File.ReadAllText(filePath).Should().Contain("deviceCommissioning");
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void Validate_DcfModel_UsesFacadeOverload()
    {
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning
            {
                NodeId = 5,
                Baudrate = 42
            }
        };
        dcf.ObjectDictionary.MandatoryObjects.Add(0x1000);
        dcf.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly
        };

        var issues = CanOpenFile.Validate(dcf);

        issues.Should().Contain(i => i.Path == "DeviceCommissioning.Baudrate");
    }

    [Fact]
    public void Validate_DcfModel_FacadeOverload_IsInvokableViaReflection()
    {
        var method = typeof(CanOpenFile).GetMethod(
            nameof(CanOpenFile.Validate),
            new[] { typeof(DeviceConfigurationFile) });
        method.Should().NotBeNull();

        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning
            {
                NodeId = 5,
                Baudrate = 42
            }
        };
        dcf.ObjectDictionary.MandatoryObjects.Add(0x1000);
        dcf.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly
        };

        var result = method!.Invoke(null, new object[] { dcf });
        result.Should().BeAssignableTo<IReadOnlyList<ValidationIssue>>();

        var issues = (IReadOnlyList<ValidationIssue>)result!;
        issues.Should().Contain(i => i.Path == "DeviceCommissioning.Baudrate");
    }

    [Fact]
    public void Validate_EdsModel_UsesFacadeOverload()
    {
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly
        };

        var issues = CanOpenFile.Validate(eds);

        issues.Should().BeEmpty();
    }

    #endregion
}
