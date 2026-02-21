namespace EdsDcfNet.Tests.Writers;

using EdsDcfNet.Models;
using EdsDcfNet.Writers;
using FluentAssertions;
using Xunit;

public class DcfWriterTests
{
    private readonly DcfWriter _writer = new();

    private DeviceConfigurationFile CreateMinimalDcf()
    {
        var dcf = new DeviceConfigurationFile
        {
            FileInfo = new EdsFileInfo
            {
                FileName = "test.dcf",
                FileVersion = 1,
                FileRevision = 0,
                EdsVersion = "4.0",
                Description = "Test DCF"
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
                NodeName = "TestNode",
                NetNumber = 1,
                NetworkName = "TestNetwork"
            },
            ObjectDictionary = new ObjectDictionary()
        };

        // Add minimal mandatory object
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

        return dcf;
    }

    #region GenerateString Tests

    [Fact]
    public void GenerateString_MinimalDcf_GeneratesValidContent()
    {
        // Arrange
        var dcf = CreateMinimalDcf();

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("[FileInfo]");
        result.Should().Contain("[DeviceInfo]");
        result.Should().Contain("[DeviceCommissioning]");
    }

    [Fact]
    public void GenerateString_FileInfo_ContainsAllFields()
    {
        // Arrange
        var dcf = CreateMinimalDcf();

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("FileName=test.dcf");
        result.Should().Contain("FileVersion=1");
        result.Should().Contain("FileRevision=0");
        result.Should().Contain("EDSVersion=4.0");
        result.Should().Contain("Description=Test DCF");
    }

    [Fact]
    public void GenerateString_DeviceInfo_ContainsVendorAndProduct()
    {
        // Arrange
        var dcf = CreateMinimalDcf();

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("VendorName=Test Vendor");
        result.Should().Contain("ProductName=Test Product");
        result.Should().Contain("VendorNumber=0x100");
        result.Should().Contain("ProductNumber=0x1001");
    }

    [Fact]
    public void GenerateString_DeviceCommissioning_ContainsNodeInfo()
    {
        // Arrange
        var dcf = CreateMinimalDcf();

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("NodeID=5");
        result.Should().Contain("Baudrate=500");
        result.Should().Contain("NodeName=TestNode");
        result.Should().Contain("NetNumber=1");
        result.Should().Contain("NetworkName=TestNetwork");
    }

    [Fact]
    public void GenerateString_ReferenceDesignators_WrittenWhenSet()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.DeviceCommissioning.NodeRefd = "N1.A2";
        dcf.DeviceCommissioning.NetRefd = "CAN-Bus 1";

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("NodeRefd=N1.A2");
        result.Should().Contain("NetRefd=CAN-Bus 1");
    }

    [Fact]
    public void GenerateString_ReferenceDesignators_OmittedWhenEmpty()
    {
        // Arrange
        var dcf = CreateMinimalDcf();

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().NotContain("NodeRefd");
        result.Should().NotContain("NetRefd");
    }

    [Fact]
    public void GenerateString_ParamRefd_WrittenOnObjectAndSubObject()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.ObjectDictionary.Objects[0x1000].ParamRefd = "X1.DevType";

        dcf.ObjectDictionary.OptionalObjects.Add(0x1018);
        dcf.ObjectDictionary.Objects[0x1018] = new CanOpenObject
        {
            Index = 0x1018,
            ParameterName = "Identity",
            ObjectType = 0x9,
            SubNumber = 1
        };
        dcf.ObjectDictionary.Objects[0x1018].SubObjects[0] = new CanOpenSubObject
        {
            SubIndex = 0,
            ParameterName = "NrOfEntries",
            ObjectType = 0x7,
            DataType = 0x0005,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "1",
            ParamRefd = "X1.Identity.Sub0"
        };

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("ParamRefd=X1.DevType");
        result.Should().Contain("ParamRefd=X1.Identity.Sub0");
    }

    [Fact]
    public void GenerateString_ParamRefd_OmittedWhenEmpty()
    {
        // Arrange
        var dcf = CreateMinimalDcf();

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().NotContain("ParamRefd");
    }

    [Fact]
    public void GenerateString_ReferenceDesignators_RoundTrip()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.DeviceCommissioning.NodeRefd = "N1.A2";
        dcf.DeviceCommissioning.NetRefd = "CAN-Bus 1";
        dcf.ObjectDictionary.Objects[0x1000].ParamRefd = "X1.DevType";

        // Act — write then re-parse
        var written = _writer.GenerateString(dcf);
        var reader = new EdsDcfNet.Parsers.DcfReader();
        var parsed = reader.ReadString(written);

        // Assert
        parsed.DeviceCommissioning.NodeRefd.Should().Be("N1.A2");
        parsed.DeviceCommissioning.NetRefd.Should().Be("CAN-Bus 1");
        parsed.ObjectDictionary.Objects[0x1000].ParamRefd.Should().Be("X1.DevType");
    }

    [Fact]
    public void GenerateString_MandatoryObjects_WritesCorrectly()
    {
        // Arrange
        var dcf = CreateMinimalDcf();

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("[MandatoryObjects]");
        result.Should().Contain("SupportedObjects=1");
        result.Should().Contain("1=0x1000");
    }

    [Fact]
    public void GenerateString_Object_WritesAllProperties()
    {
        // Arrange
        var dcf = CreateMinimalDcf();

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("[1000]");
        result.Should().Contain("ParameterName=Device Type");
        result.Should().Contain("ObjectType=0x7");
        result.Should().Contain("DataType=0x7");
        result.Should().Contain("AccessType=ro");
        result.Should().Contain("DefaultValue=0x191");
        result.Should().Contain("PDOMapping=0");
    }

    [Fact]
    public void GenerateString_ObjectWithSubObjects_WritesSubObjects()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.ObjectDictionary.MandatoryObjects.Add(0x1018);
        dcf.ObjectDictionary.Objects[0x1018] = new CanOpenObject
        {
            Index = 0x1018,
            ParameterName = "Identity Object",
            ObjectType = 0x9,
            SubNumber = 2
        };

        dcf.ObjectDictionary.Objects[0x1018].SubObjects[0] = new CanOpenSubObject
        {
            SubIndex = 0,
            ParameterName = "Number of Entries",
            ObjectType = 0x7,
            DataType = 0x0005,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "2",
            PdoMapping = false
        };

        dcf.ObjectDictionary.Objects[0x1018].SubObjects[1] = new CanOpenSubObject
        {
            SubIndex = 1,
            ParameterName = "Vendor ID",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x100",
            PdoMapping = false
        };

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("[1018]");
        result.Should().Contain("SubNumber=2");
        result.Should().Contain("[1018sub0]");
        result.Should().Contain("[1018sub1]");
    }

    [Fact]
    public void GenerateString_Comments_WritesCorrectly()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.Comments = new Comments
        {
            Lines = 2
        };
        dcf.Comments.CommentLines[1] = "First comment";
        dcf.Comments.CommentLines[2] = "Second comment";

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("[Comments]");
        result.Should().Contain("Lines=2");
        result.Should().Contain("Line1=First comment");
        result.Should().Contain("Line2=Second comment");
    }

    [Fact]
    public void GenerateString_BaudRates_WritesAllFlags()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.DeviceInfo.SupportedBaudRates.BaudRate10 = true;
        dcf.DeviceInfo.SupportedBaudRates.BaudRate125 = true;
        dcf.DeviceInfo.SupportedBaudRates.BaudRate250 = true;
        dcf.DeviceInfo.SupportedBaudRates.BaudRate500 = true;

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("BaudRate_10=1");
        result.Should().Contain("BaudRate_20=0");
        result.Should().Contain("BaudRate_125=1");
        result.Should().Contain("BaudRate_250=1");
        result.Should().Contain("BaudRate_500=1");
    }

    [Fact]
    public void GenerateString_AccessTypes_FormatsCorrectly()
    {
        // Arrange
        var dcf = CreateMinimalDcf();

        dcf.ObjectDictionary.OptionalObjects.Add(0x2000);
        dcf.ObjectDictionary.Objects[0x2000] = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "RW Object",
            ObjectType = 0x7,
            DataType = 0x0005,
            AccessType = AccessType.ReadWrite,
            DefaultValue = "0",
            PdoMapping = false
        };

        dcf.ObjectDictionary.OptionalObjects.Add(0x2001);
        dcf.ObjectDictionary.Objects[0x2001] = new CanOpenObject
        {
            Index = 0x2001,
            ParameterName = "WO Object",
            ObjectType = 0x7,
            DataType = 0x0005,
            AccessType = AccessType.WriteOnly,
            DefaultValue = "0",
            PdoMapping = false
        };

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("[2000]");
        result.Should().Contain("AccessType=rw");
        result.Should().Contain("[2001]");
        result.Should().Contain("AccessType=wo");
    }

    [Fact]
    public void GenerateString_HexValues_FormatsWithPrefix()
    {
        // Arrange
        var dcf = CreateMinimalDcf();

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("VendorNumber=0x100");
        result.Should().Contain("ProductNumber=0x1001");
        result.Should().Contain("DataType=0x7");
    }

    [Fact]
    public void GenerateString_ParameterValue_OverridesDefaultValue()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.ObjectDictionary.Objects[0x1000].ParameterValue = "0x999";

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("ParameterValue=0x999");
    }

    [Fact]
    public void GenerateString_OptionalObjects_WritesCorrectly()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.ObjectDictionary.OptionalObjects.Add(0x1008);
        dcf.ObjectDictionary.Objects[0x1008] = new CanOpenObject
        {
            Index = 0x1008,
            ParameterName = "Device Name",
            ObjectType = 0x7,
            DataType = 0x0009,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "TestDevice",
            PdoMapping = false
        };

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("[OptionalObjects]");
        result.Should().Contain("SupportedObjects=1");
        result.Should().Contain("1=0x1008");
        result.Should().Contain("[1008]");
    }

    [Fact]
    public void GenerateString_ManufacturerObjects_WritesCorrectly()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.ObjectDictionary.ManufacturerObjects.Add(0x2000);
        dcf.ObjectDictionary.Objects[0x2000] = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Custom Object",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadWrite,
            DefaultValue = "0",
            PdoMapping = true
        };

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("[ManufacturerObjects]");
        result.Should().Contain("SupportedObjects=1");
        result.Should().Contain("1=0x2000");
        result.Should().Contain("[2000]");
    }

    [Fact]
    public void GenerateString_DummyUsage_WritesCorrectly()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.ObjectDictionary.DummyUsage[2] = true;
        dcf.ObjectDictionary.DummyUsage[5] = true;

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("[DummyUsage]");
        result.Should().Contain("Dummy0002=1");
        result.Should().Contain("Dummy0005=1");
    }

    [Fact]
    public void GenerateString_CANopenSafetySupported_WrittenWhenTrue()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.DeviceInfo.CANopenSafetySupported = true;

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("CANopenSafetySupported=1");
    }

    [Fact]
    public void GenerateString_CANopenSafetySupported_OmittedWhenFalse()
    {
        // Arrange
        var dcf = CreateMinimalDcf();

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().NotContain("CANopenSafetySupported");
    }

    [Fact]
    public void GenerateString_SrdoMapping_WrittenOnObjectAndSubObject()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.ObjectDictionary.OptionalObjects.Add(0x6100);
        dcf.ObjectDictionary.Objects[0x6100] = new CanOpenObject
        {
            Index = 0x6100,
            ParameterName = "SRDO Input",
            ObjectType = 0x8,
            DataType = 0x0005,
            AccessType = AccessType.ReadOnly,
            PdoMapping = true,
            SrdoMapping = true,
            InvertedSrad = "0x610101",
            SubNumber = 1
        };
        dcf.ObjectDictionary.Objects[0x6100].SubObjects[0] = new CanOpenSubObject
        {
            SubIndex = 0,
            ParameterName = "Number of Entries",
            ObjectType = 0x7,
            DataType = 0x0005,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "1",
            PdoMapping = false,
            SrdoMapping = false
        };
        dcf.ObjectDictionary.Objects[0x6100].SubObjects[1] = new CanOpenSubObject
        {
            SubIndex = 1,
            ParameterName = "SRDO Input 1",
            ObjectType = 0x7,
            DataType = 0x0005,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0",
            PdoMapping = true,
            SrdoMapping = true,
            InvertedSrad = "0x610101"
        };

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("[6100]");
        result.Should().Contain("SRDOMapping=1");
        result.Should().Contain("InvertedSRAD=0x610101");
    }

    [Fact]
    public void GenerateString_InvertedSrad_OmittedWhenEmpty()
    {
        // Arrange
        var dcf = CreateMinimalDcf();

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().NotContain("InvertedSRAD");
        result.Should().NotContain("SRDOMapping");
    }

    [Fact]
    public void GenerateString_SafetyProperties_RoundTrip()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.DeviceInfo.CANopenSafetySupported = true;
        dcf.ObjectDictionary.OptionalObjects.Add(0x6100);
        dcf.ObjectDictionary.Objects[0x6100] = new CanOpenObject
        {
            Index = 0x6100,
            ParameterName = "SRDO Input",
            ObjectType = 0x8,
            DataType = 0x0005,
            AccessType = AccessType.ReadOnly,
            PdoMapping = true,
            SrdoMapping = true,
            InvertedSrad = "0x610101",
            SubNumber = 1
        };
        dcf.ObjectDictionary.Objects[0x6100].SubObjects[0] = new CanOpenSubObject
        {
            SubIndex = 0,
            ParameterName = "Number of Entries",
            ObjectType = 0x7,
            DataType = 0x0005,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "1",
            PdoMapping = false,
            SrdoMapping = false
        };
        dcf.ObjectDictionary.Objects[0x6100].SubObjects[1] = new CanOpenSubObject
        {
            SubIndex = 1,
            ParameterName = "SRDO Input 1",
            ObjectType = 0x7,
            DataType = 0x0005,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0",
            PdoMapping = true,
            SrdoMapping = true,
            InvertedSrad = "0x610101"
        };

        // Act - Write then re-parse
        var written = _writer.GenerateString(dcf);
        var reader = new EdsDcfNet.Parsers.DcfReader();
        var parsed = reader.ReadString(written);

        // Assert
        parsed.DeviceInfo.CANopenSafetySupported.Should().BeTrue();

        var obj = parsed.ObjectDictionary.Objects[0x6100];
        obj.SrdoMapping.Should().BeTrue();
        obj.InvertedSrad.Should().Be("0x610101");

        obj.SubObjects[0].SrdoMapping.Should().BeFalse();
        obj.SubObjects[0].InvertedSrad.Should().BeNullOrEmpty();

        obj.SubObjects[1].SrdoMapping.Should().BeTrue();
        obj.SubObjects[1].InvertedSrad.Should().Be("0x610101");
    }

    [Fact]
    public void GenerateString_ObjectLinksInAdditionalSections_FilteredForExistingObjects()
    {
        // Arrange — simulate an AdditionalSections entry for ObjectLinks of an existing object
        var dcf = CreateMinimalDcf();
        dcf.ObjectDictionary.Objects[0x1000].ObjectLinks.Add(0x2000);
        dcf.AdditionalSections["1000ObjectLinks"] = new Dictionary<string, string>
        {
            { "ObjectLinks", "1" },
            { "1", "0x2000" }
        };

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert — ObjectLinks written via the object, not duplicated from AdditionalSections
        var matches = result.Split(new[] { "[1000ObjectLinks]" }, StringSplitOptions.None);
        matches.Should().HaveCount(2, "ObjectLinks section should appear exactly once");
    }

    [Fact]
    public void GenerateString_ObjectLinksInAdditionalSections_KeptForOrphanObjects()
    {
        // Arrange — ObjectLinks for a non-existing object should pass through
        var dcf = CreateMinimalDcf();
        dcf.AdditionalSections["9999ObjectLinks"] = new Dictionary<string, string>
        {
            { "ObjectLinks", "1" },
            { "1", "0x1000" }
        };

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("[9999ObjectLinks]");
    }

    [Fact]
    public void GenerateString_NonObjectLinksAdditionalSection_NotFiltered()
    {
        // Arrange — a section that doesn't end with "ObjectLinks"
        var dcf = CreateMinimalDcf();
        dcf.AdditionalSections["VendorSpecific"] = new Dictionary<string, string>
        {
            { "Key", "Value" }
        };

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("[VendorSpecific]");
        result.Should().Contain("Key=Value");
    }

    [Fact]
    public void GenerateString_EmptyPrefixObjectLinks_NotFiltered()
    {
        // Arrange — section named just "ObjectLinks" with no hex prefix
        var dcf = CreateMinimalDcf();
        dcf.AdditionalSections["ObjectLinks"] = new Dictionary<string, string>
        {
            { "ObjectLinks", "1" },
            { "1", "0x1000" }
        };

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("[ObjectLinks]");
    }

    [Fact]
    public void GenerateString_NonHexPrefixObjectLinks_NotFiltered()
    {
        // Arrange — section ending with "ObjectLinks" but non-hex prefix
        var dcf = CreateMinimalDcf();
        dcf.AdditionalSections["ZZZZObjectLinks"] = new Dictionary<string, string>
        {
            { "ObjectLinks", "1" },
            { "1", "0x1000" }
        };

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("[ZZZZObjectLinks]");
    }

    #endregion

    #region WriteFile Tests

    [Fact]
    public void WriteFile_ValidPath_CreatesFile()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            _writer.WriteFile(dcf, tempFile);

            // Assert
            File.Exists(tempFile).Should().BeTrue();
            var content = File.ReadAllText(tempFile);
            content.Should().Contain("[FileInfo]");
            content.Should().Contain("[DeviceCommissioning]");
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteFile_InvalidPath_ThrowsDcfWriteException()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        var invalidPath = "/invalid/path/that/does/not/exist/test.dcf";

        // Act
        var act = () => _writer.WriteFile(dcf, invalidPath);

        // Assert
        act.Should().Throw<EdsDcfNet.Exceptions.DcfWriteException>()
            .WithMessage("*Failed to write DCF file*");
    }

    [Fact]
    public void WriteFile_NonAsciiCharacters_PreservesCharacters()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.DeviceInfo.VendorName = "Müller GmbH & Söhne";
        dcf.DeviceInfo.ProductName = "Schütz-Relä Ä5";
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            _writer.WriteFile(dcf, tempFile);

            // Assert
            var content = File.ReadAllText(tempFile, System.Text.Encoding.UTF8);
            content.Should().Contain("Müller GmbH & Söhne");
            content.Should().Contain("Schütz-Relä Ä5");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    #endregion

    #region DynamicChannels Tests

    [Fact]
    public void GenerateString_DynamicChannels_WritesSection()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.DynamicChannels = new DynamicChannels
        {
            Segments = new List<DynamicChannelSegment>
            {
                new DynamicChannelSegment
                {
                    Type = 0x0007,
                    Dir = AccessType.ReadOnly,
                    Range = "0xA080-0xA0BF",
                    PPOffset = 0
                },
                new DynamicChannelSegment
                {
                    Type = 0x0005,
                    Dir = AccessType.ReadWriteOutput,
                    Range = "0xA0C0-0xA0FF",
                    PPOffset = 64
                }
            }
        };

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("[DynamicChannels]");
        result.Should().Contain("NrOfSeg=2");
        result.Should().Contain("Type1=0x7");
        result.Should().Contain("Dir1=ro");
        result.Should().Contain("Range1=0xA080-0xA0BF");
        result.Should().Contain("PPOffset1=0");
        result.Should().Contain("Type2=0x5");
        result.Should().Contain("Dir2=rww");
        result.Should().Contain("Range2=0xA0C0-0xA0FF");
        result.Should().Contain("PPOffset2=64");
    }

    [Fact]
    public void GenerateString_NoDynamicChannels_OmitsSection()
    {
        // Arrange
        var dcf = CreateMinimalDcf();

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().NotContain("[DynamicChannels]");
    }

    [Fact]
    public void GenerateString_DynamicChannels_RoundTrip()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.DynamicChannels = new DynamicChannels
        {
            Segments = new List<DynamicChannelSegment>
            {
                new DynamicChannelSegment
                {
                    Type = 0x0007,
                    Dir = AccessType.ReadOnly,
                    Range = "0xA080-0xA0BF",
                    PPOffset = 0
                }
            }
        };

        // Act
        var output = _writer.GenerateString(dcf);
        var reader = new EdsDcfNet.Parsers.DcfReader();
        var parsed = reader.ReadString(output);

        // Assert
        parsed.DynamicChannels.Should().NotBeNull();
        parsed.DynamicChannels!.Segments.Should().HaveCount(1);
        parsed.DynamicChannels.Segments[0].Type.Should().Be(0x0007);
        parsed.DynamicChannels.Segments[0].Dir.Should().Be(AccessType.ReadOnly);
        parsed.DynamicChannels.Segments[0].Range.Should().Be("0xA080-0xA0BF");
        parsed.DynamicChannels.Segments[0].PPOffset.Should().Be(0);
    }

    #endregion

    #region Tools Tests

    [Fact]
    public void GenerateString_Tools_WritesSections()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.Tools = new List<ToolInfo>
        {
            new ToolInfo { Name = "EDS Checker", Command = "checker.exe $EDS" },
            new ToolInfo { Name = "Configurator", Command = "config.exe $DCF $NODEID" }
        };

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().Contain("[Tools]");
        result.Should().Contain("Items=2");
        result.Should().Contain("[Tool1]");
        result.Should().Contain("Name=EDS Checker");
        result.Should().Contain("Command=checker.exe $EDS");
        result.Should().Contain("[Tool2]");
        result.Should().Contain("Name=Configurator");
        result.Should().Contain("Command=config.exe $DCF $NODEID");
    }

    [Fact]
    public void GenerateString_NoTools_OmitsSection()
    {
        // Arrange
        var dcf = CreateMinimalDcf();

        // Act
        var result = _writer.GenerateString(dcf);

        // Assert
        result.Should().NotContain("[Tools]");
    }

    [Fact]
    public void GenerateString_Tools_RoundTrip()
    {
        // Arrange
        var dcf = CreateMinimalDcf();
        dcf.Tools = new List<ToolInfo>
        {
            new ToolInfo { Name = "EDS Checker", Command = "checker.exe $EDS" },
            new ToolInfo { Name = "Configurator", Command = "config.exe $DCF $NODEID" }
        };

        // Act
        var output = _writer.GenerateString(dcf);
        var reader = new EdsDcfNet.Parsers.DcfReader();
        var parsed = reader.ReadString(output);

        // Assert
        parsed.Tools.Should().HaveCount(2);
        parsed.Tools[0].Name.Should().Be("EDS Checker");
        parsed.Tools[0].Command.Should().Be("checker.exe $EDS");
        parsed.Tools[1].Name.Should().Be("Configurator");
        parsed.Tools[1].Command.Should().Be("config.exe $DCF $NODEID");
    }

    #endregion
}
