namespace EdsDcfNet.Tests.Writers;

using EdsDcfNet.Models;
using EdsDcfNet.Writers;

public class XdcWriterTests
{
    private readonly XdcWriter _writer = new();

    private static DeviceConfigurationFile CreateSampleDcf()
    {
        var dcf = new DeviceConfigurationFile
        {
            FileInfo = new EdsFileInfo
            {
                FileName = "test.xdc",
                FileVersion = 1,
                CreatedBy = "TestCreator",
                CreationDate = "01-15-2025"
            },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Test Vendor",
                VendorNumber = 0x00000100,
                ProductName = "Test Product",
                ProductNumber = 0x00001001,
                Granularity = 8,
                NrOfRxPdo = 2,
                NrOfTxPdo = 2,
                SimpleBootUpSlave = true,
                SupportedBaudRates = new BaudRates
                {
                    BaudRate250 = true,
                    BaudRate500 = true
                }
            },
            DeviceCommissioning = new DeviceCommissioning
            {
                NodeId = 3,
                NodeName = "MyDevice",
                Baudrate = 500,
                NetNumber = 1,
                NetworkName = "CANopen Network",
                CANopenManager = false
            }
        };

        var obj = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x00000000",
            PdoMapping = false,
            ParameterValue = "0x00000191",
            Denotation = "MyDeviceType"
        };
        dcf.ObjectDictionary.Objects[0x1000] = obj;
        dcf.ObjectDictionary.MandatoryObjects.Add(0x1000);

        return dcf;
    }

    #region GenerateString Tests

    [Fact]
    public void GenerateString_ContainsISO15745ProfileContainer()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleDcf());

        // Assert
        result.Should().Contain("ISO15745ProfileContainer");
    }

    [Fact]
    public void GenerateString_ContainsTwoProfiles()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleDcf());

        // Assert
        result.Should().Contain("ProfileBody_Device_CANopen");
        result.Should().Contain("ProfileBody_CommunicationNetwork_CANopen");
    }

    [Fact]
    public void GenerateString_ParameterValue_WrittenAsActualValue()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleDcf());

        // Assert
        result.Should().Contain("actualValue=\"0x00000191\"");
    }

    [Fact]
    public void GenerateString_Denotation_WrittenCorrectly()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleDcf());

        // Assert
        result.Should().Contain("denotation=\"MyDeviceType\"");
    }

    [Fact]
    public void GenerateString_DeviceCommissioning_WrittenCorrectly()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleDcf());

        // Assert
        result.Should().Contain("deviceCommissioning");
        result.Should().Contain("nodeID=\"3\"");
        result.Should().Contain("nodeName=\"MyDevice\"");
        result.Should().Contain("actualBaudRate=\"500 Kbps\"");
        result.Should().Contain("networkNumber=\"1\"");
        result.Should().Contain("networkName=\"CANopen Network\"");
        result.Should().Contain("CANopenManager=\"false\"");
    }

    [Fact]
    public void GenerateString_DeviceCommissioning_Null_ElementOmitted()
    {
        // Arrange
        var dcf = CreateSampleDcf();
        dcf.DeviceCommissioning = null!;

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().NotContain("deviceCommissioning");
    }

    [Fact]
    public void GenerateString_NoActualValue_AttributeOmitted()
    {
        // Arrange
        var dcf = CreateSampleDcf();
        dcf.ObjectDictionary.Objects[0x1000].ParameterValue = null;

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().NotContain("actualValue");
    }

    [Fact]
    public void WriteFile_CreatesValidXmlFile()
    {
        // Arrange
        var dcf = CreateSampleDcf();
        var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xdc");

        try
        {
            // Act
            _writer.WriteFile(dcf, filePath);

            // Assert
            File.Exists(filePath).Should().BeTrue();
            var content = File.ReadAllText(filePath);
            content.Should().Contain("ISO15745ProfileContainer");
            content.Should().Contain("deviceCommissioning");
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void GenerateString_SubObjectActualValue_WrittenCorrectly()
    {
        // Arrange
        var dcf = CreateSampleDcf();
        var parentObj = new CanOpenObject
        {
            Index = 0x1018,
            ParameterName = "Identity Object",
            ObjectType = 0x9,
            SubNumber = 1
        };
        var sub = new CanOpenSubObject
        {
            SubIndex = 0,
            ParameterName = "Number of Entries",
            ObjectType = 0x7,
            DataType = 0x0005,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "4",
            ParameterValue = "4"
        };
        parentObj.SubObjects[0] = sub;
        dcf.ObjectDictionary.Objects[0x1018] = parentObj;
        dcf.ObjectDictionary.OptionalObjects.Add(0x1018);

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("CANopenSubObject");
        result.Should().Contain("actualValue=\"4\"");
    }

    [Fact]
    public void GenerateString_SubObjectDenotation_WrittenCorrectly()
    {
        // Arrange — sub-object with Denotation set (covers AddCanOpenSubObjectXdcAttributes denotation branch)
        var dcf = CreateSampleDcf();
        var parentObj = new CanOpenObject
        {
            Index = 0x1018, ParameterName = "Identity Object", ObjectType = 0x9, SubNumber = 2
        };
        var sub = new CanOpenSubObject
        {
            SubIndex = 1,
            ParameterName = "Vendor ID",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x00000100",
            ParameterValue = "0x00000100",
            Denotation = "VendorIdentifier"
        };
        parentObj.SubObjects[1] = sub;
        dcf.ObjectDictionary.Objects[0x1018] = parentObj;
        dcf.ObjectDictionary.OptionalObjects.Add(0x1018);

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("denotation=\"VendorIdentifier\"");
    }

    [Fact]
    public void GenerateString_DcfWithAdditionalSections_CreateEdsViewCoversAllPaths()
    {
        // Arrange — DCF with AdditionalSections to cover CreateEdsView kvp loop (line 114)
        var dcf = CreateSampleDcf();
        dcf.AdditionalSections["CustomSection"] = new System.Collections.Generic.Dictionary<string, string> { { "key", "value" } };

        // Act — should not throw; AdditionalSections are mapped through CreateEdsView
        var result = _writer.GenerateString(dcf);

        // Assert — output is valid
        result.Should().Contain("ISO15745ProfileContainer");
    }

    #endregion

    #region NodeId validation

    [Theory]
    [InlineData(128)]
    [InlineData(255)]
    public void GenerateString_OutOfRangeNodeId_ThrowsInvalidOperationException(byte nodeId)
    {
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = nodeId }
        };

        var act = () => _writer.GenerateString(dcf);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*NodeId*");
    }

    [Fact]
    public void GenerateString_NodeIdZero_OmitsDeviceCommissioningElement()
    {
        // NodeId == 0 means "no commissioning configured"; the element must be omitted
        // so that read→write round-trips for XDC files without commissioning succeed.
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = 0 }
        };

        var result = _writer.GenerateString(dcf);

        result.Should().NotContain("deviceCommissioning");
    }

    #endregion
}
