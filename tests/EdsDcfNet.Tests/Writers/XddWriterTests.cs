namespace EdsDcfNet.Tests.Writers;

using System.Xml.Linq;
using EdsDcfNet.Models;
using EdsDcfNet.Writers;

public class XddWriterTests
{
    private readonly XddWriter _writer = new();

    private static ElectronicDataSheet CreateSampleEds()
    {
        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo
            {
                FileName = "test.xdd",
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
            }
        };

        var obj = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x00000191",
            PdoMapping = false
        };
        eds.ObjectDictionary.Objects[0x1000] = obj;
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);

        return eds;
    }

    #region WriteFile Tests

    [Fact]
    public void WriteFile_CreatesValidXmlFile()
    {
        // Arrange
        var eds = CreateSampleEds();
        var filePath = Path.GetTempFileName() + ".xdd";

        try
        {
            // Act
            _writer.WriteFile(eds, filePath);

            // Assert
            File.Exists(filePath).Should().BeTrue();
            var content = File.ReadAllText(filePath);
            content.Should().Contain("ISO15745ProfileContainer");
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    #endregion

    #region GenerateString Tests

    [Fact]
    public void GenerateString_ContainsISO15745ProfileContainer()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleEds());

        // Assert
        result.Should().Contain("ISO15745ProfileContainer");
    }

    [Fact]
    public void GenerateString_ContainsTwoProfiles()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleEds());

        // Assert
        result.Should().Contain("ProfileBody_Device_CANopen");
        result.Should().Contain("ProfileBody_CommunicationNetwork_CANopen");
    }

    [Fact]
    public void GenerateString_DeviceIdentity_CorrectVendorAndProduct()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleEds());

        // Assert
        result.Should().Contain("<vendorName>Test Vendor</vendorName>");
        result.Should().Contain("<productName>Test Product</productName>");
        result.Should().Contain("0x00000100");
        result.Should().Contain("0x00001001");
    }

    [Fact]
    public void GenerateString_CANopenObjectList_CorrectCounts()
    {
        // Arrange
        var eds = CreateSampleEds();

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("mandatoryObjects=\"1\"");
        result.Should().Contain("optionalObjects=\"0\"");
        result.Should().Contain("manufacturerObjects=\"0\"");
    }

    [Fact]
    public void GenerateString_Objects_AllAttributesPresent()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleEds());

        // Assert
        result.Should().Contain("index=\"1000\"");
        result.Should().Contain("name=\"Device Type\"");
        result.Should().Contain("objectType=\"7\"");
        result.Should().Contain("dataType=\"0007\"");
        result.Should().Contain("accessType=\"ro\"");
        result.Should().Contain("defaultValue=\"0x00000191\"");
        result.Should().Contain("PDOmapping=\"no\"");
    }

    [Fact]
    public void GenerateString_SubObjects_WrittenCorrectly()
    {
        // Arrange
        var eds = CreateSampleEds();
        var parentObj = new CanOpenObject
        {
            Index = 0x1018,
            ParameterName = "Identity Object",
            ObjectType = 0x9,
            SubNumber = 2
        };
        var subObj = new CanOpenSubObject
        {
            SubIndex = 0,
            ParameterName = "Number of Entries",
            ObjectType = 0x7,
            DataType = 0x0005,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "4"
        };
        parentObj.SubObjects[0] = subObj;
        eds.ObjectDictionary.Objects[0x1018] = parentObj;
        eds.ObjectDictionary.OptionalObjects.Add(0x1018);

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("<CANopenSubObject");
        result.Should().Contain("subIndex=\"00\"");
        result.Should().Contain("name=\"Number of Entries\"");
    }

    [Fact]
    public void GenerateString_DummyUsage_WrittenCorrectly()
    {
        // Arrange
        var eds = CreateSampleEds();
        eds.ObjectDictionary.DummyUsage[0x0002] = true;
        eds.ObjectDictionary.DummyUsage[0x0005] = false;

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("dummyUsage");
        result.Should().Contain("Dummy0002=1");
        result.Should().Contain("Dummy0005=0");
    }

    [Fact]
    public void GenerateString_SupportedBaudRates_WrittenAsStrings()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleEds());

        // Assert
        result.Should().Contain("250 Kbps");
        result.Should().Contain("500 Kbps");
    }

    [Fact]
    public void GenerateString_ApplicationProcessXml_PreservedRoundTrip()
    {
        // Arrange
        var eds = CreateSampleEds();
        eds.ApplicationProcessXml = "<ApplicationProcess><dataTypeList/></ApplicationProcess>";

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("ApplicationProcess");
        result.Should().Contain("dataTypeList");
    }

    [Fact]
    public void GenerateString_AccessType_ReadWriteInput_WrittenAsRw()
    {
        // Arrange
        var eds = CreateSampleEds();
        eds.ObjectDictionary.Objects[0x1000].AccessType = AccessType.ReadWriteInput;

        // Act
        var result = _writer.GenerateString(eds);

        // Assert — ReadWriteInput has no XDD equivalent, maps to "rw"
        result.Should().Contain("accessType=\"rw\"");
    }

    [Fact]
    public void GenerateString_PdoMapping_false_WrittenAsNo()
    {
        // Arrange
        var eds = CreateSampleEds();
        eds.ObjectDictionary.Objects[0x1000].PdoMapping = false;

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("PDOmapping=\"no\"");
    }

    [Fact]
    public void GenerateString_PdoMapping_true_WrittenAsOptional()
    {
        // Arrange
        var eds = CreateSampleEds();
        eds.ObjectDictionary.Objects[0x1000].PdoMapping = true;

        // Act
        var result = _writer.GenerateString(eds);

        // Assert
        result.Should().Contain("PDOmapping=\"optional\"");
    }

    [Fact]
    public void GenerateString_ValidXml_CanBeParsedBack()
    {
        // Act
        var result = _writer.GenerateString(CreateSampleEds());

        // Assert — output must be parseable as valid XML
        var act = () => XDocument.Parse(result);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateString_DateConversion_EdsToXsd()
    {
        // Arrange — CreationDate in EDS format "MM-DD-YYYY"
        var eds = CreateSampleEds();
        eds.FileInfo.CreationDate = "03-15-2025";

        // Act
        var result = _writer.GenerateString(eds);

        // Assert — date written in XSD format "YYYY-MM-DD"
        result.Should().Contain("fileCreationDate=\"2025-03-15\"");
    }

    #endregion
}
