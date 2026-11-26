namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Utilities;

public partial class DcfReaderTests
{
    #region Case-Insensitivity Tests

    [Fact]
    public void ReadString_CaseInsensitiveSections_ParsesCorrectly()
    {
        // Arrange – mixed-case section names
        var content = @"
[fileinfo]
FileName=case_test.dcf
FileVersion=1

[deviceinfo]
VendorName=Test
ProductName=CaseTest
VendorNumber=0x1

[devicecommissioning]
NodeID=7
Baudrate=250

[mandatoryobjects]
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
        var result = _reader.ReadString(content);

        // Assert
        result.FileInfo.FileName.Should().Be("case_test.dcf");
        result.DeviceInfo.VendorName.Should().Be("Test");
        result.DeviceCommissioning.NodeId.Should().Be(7);
        result.ObjectDictionary.MandatoryObjects.Should().Contain((ushort)0x1000);
    }

    #endregion

    #region AccessType Parsing in Objects

    [Theory]
    [InlineData("ro", AccessType.ReadOnly)]
    [InlineData("wo", AccessType.WriteOnly)]
    [InlineData("rw", AccessType.ReadWrite)]
    [InlineData("rwr", AccessType.ReadWriteInput)]
    [InlineData("rww", AccessType.ReadWriteOutput)]
    [InlineData("const", AccessType.Constant)]
    public void ReadString_AccessType_ParsedCorrectly(string accessTypeStr, AccessType expected)
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: $@"
[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=AccessTest
ObjectType=0x7
DataType=0x0007
AccessType={accessTypeStr}
DefaultValue=0
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.Objects[0x2000].AccessType.Should().Be(expected);
    }

    #endregion

    #region Full Feature Fixture Tests

    [Fact]
    public void ReadFile_FullFeatures_CommissioningWithLssSerial()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/full_features.dcf");

        // Assert
        result.DeviceCommissioning.CANopenManager.Should().BeTrue();
        result.DeviceCommissioning.LssSerialNumber.Should().Be(12345678u);
    }

    [Fact]
    public void ReadFile_FullFeatures_DcfSpecificFieldsOnObjects()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/full_features.dcf");

        // Assert
        var obj2000 = result.ObjectDictionary.Objects[0x2000];
        obj2000.ParameterValue.Should().Be("42");
        obj2000.UploadFile.Should().Be("status_upload.bin");
        obj2000.DownloadFile.Should().Be("status_download.bin");
        obj2000.ObjFlags.Should().Be(3u);
    }

    [Fact]
    public void ReadFile_FullFeatures_ObjectLinksOnObject()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/full_features.dcf");

        // Assert
        var obj2000 = result.ObjectDictionary.Objects[0x2000];
        obj2000.ObjectLinks.Should().HaveCount(2);
        obj2000.ObjectLinks.Should().Contain((ushort)0x2100);
        obj2000.ObjectLinks.Should().Contain((ushort)0x1000);

        // ObjectLinks sections for existing objects are handled on the object itself
        // and filtered from AdditionalSections.
        result.AdditionalSections.Should().NotContainKey("2000ObjectLinks");
    }

    [Fact]
    public void ReadFile_FullFeatures_CompactValueAndDenotation()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/full_features.dcf");

        // Assert
        var obj2100 = result.ObjectDictionary.Objects[0x2100];
        obj2100.CompactSubObj.Should().Be(3);

        // Value section applied
        obj2100.SubObjects[1].ParameterValue.Should().Be("10");
        obj2100.SubObjects[2].ParameterValue.Should().Be("20");
        obj2100.SubObjects[3].ParameterValue.Should().Be("30");

        // Denotation section applied
        obj2100.SubObjects[1].Denotation.Should().Be("Config A");
        obj2100.SubObjects[2].Denotation.Should().Be("Config B");
        obj2100.SubObjects[3].Denotation.Should().Be("Config C");
    }

    [Fact]
    public void ReadFile_FullFeatures_UnknownSectionPreserved()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/full_features.dcf");

        // Assert
        result.AdditionalSections.Should().ContainKey("VendorSpecificSection");
        result.AdditionalSections["VendorSpecificSection"]["CustomKey1"].Should().Be("CustomValue1");
        result.AdditionalSections["VendorSpecificSection"]["VersionInfo"].Should().Be("1.2.3");
    }

    [Fact]
    public void ReadFile_FullFeatures_DummyUsage()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/full_features.dcf");

        // Assert
        result.ObjectDictionary.DummyUsage[2].Should().BeTrue();
        result.ObjectDictionary.DummyUsage[3].Should().BeFalse();
        result.ObjectDictionary.DummyUsage[5].Should().BeTrue();
        result.ObjectDictionary.DummyUsage[7].Should().BeTrue();
    }

    [Fact]
    public void ReadFile_FullFeatures_Comments()
    {
        // Act
        var result = _reader.ReadFile("Fixtures/full_features.dcf");

        // Assert
        result.Comments.Should().NotBeNull();
        result.Comments!.Lines.Should().Be(2);
        result.Comments.CommentLines[1].Should().Be("Full-featured DCF test file");
        result.Comments.CommentLines[2].Should().Be("Contains all supported DCF extensions");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ReadString_ObjectSectionMissing_ObjectNotAdded()
    {
        // Arrange – Object listed but has no definition section
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x9999
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.OptionalObjects.Should().Contain((ushort)0x9999);
        result.ObjectDictionary.Objects.Should().NotContainKey((ushort)0x9999);
    }

    [Fact]
    public void ReadString_EmptyObjectDictionary_ParsesWithoutError()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: "").Replace(@"
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
", @"
[MandatoryObjects]
SupportedObjects=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.MandatoryObjects.Should().BeEmpty();
        result.ObjectDictionary.Objects.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_DuplicateIndexInMultipleLists_ObjectOnlyOnce()
    {
        // Arrange – same index in both mandatory and optional (unusual but possible)
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x1000
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        // Distinct() in DcfReader ensures only one object entry
        result.ObjectDictionary.Objects.Should().ContainKey((ushort)0x1000);
    }

    #endregion

    #region Negative/Error-Path Tests

    [Fact]
    public void ReadString_EmptyContent_RequiresMandatoryDeviceInfo()
    {
        // Arrange
        var content = "";

        // Act
        var act = () => _reader.ReadString(content);

        // Assert - empty content lacks [DeviceInfo] which is mandatory
        act.Should().Throw<EdsParseException>()
            .WithMessage("*DeviceInfo*");
    }

    [Fact]
    public void ReadString_WhitespaceOnlyContent_RequiresMandatoryDeviceInfo()
    {
        // Arrange
        var content = "   \n\n  \t  ";

        // Act
        var act = () => _reader.ReadString(content);

        // Assert - whitespace-only content lacks [DeviceInfo]
        act.Should().Throw<EdsParseException>()
            .WithMessage("*DeviceInfo*");
    }

    [Fact]
    public void ReadString_InvalidAccessType_ParsesAsDefault()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Invalid AccessType
ObjectType=0x7
DataType=0x0007
AccessType=invalid
DefaultValue=0
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        // Parser uses ValueConverter.ParseAccessType which handles unknown values
        var obj = result.ObjectDictionary.Objects[0x2000];
        obj.AccessType.Should().Be(AccessType.ReadOnly); // default fallback
    }

    [Fact]
    public void ReadString_NonHexObjectIndex_SkipsEntry()
    {
        // Arrange – Non-hex index in object list. Lenient mode skips the entry (#557).
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=NotHex
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DeviceInfo.VendorName.Should().Be("Test Vendor");
        result.ObjectDictionary.OptionalObjects.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_OverflowObjectIndex_SkipsEntry()
    {
        // Arrange – Value exceeds ushort range. Lenient mode skips the entry (#557).
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x10000
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DeviceInfo.VendorName.Should().Be("Test Vendor");
        result.ObjectDictionary.OptionalObjects.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_SupportedObjectsCountMismatch_IgnoresExtra()
    {
        // Arrange – SupportedObjects says 2 but list 3
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=2
1=0x1008
2=0x1009
3=0x100A

[1008]
ParameterName=Object 1008
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0
PDOMapping=0

[1009]
ParameterName=Object 1009
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0
PDOMapping=0

[100A]
ParameterName=Object 100A
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        // Parser respects SupportedObjects count, only loads first 2
        result.ObjectDictionary.OptionalObjects.Should().HaveCount(2);
        result.ObjectDictionary.OptionalObjects.Should().Contain(0x1008);
        result.ObjectDictionary.OptionalObjects.Should().Contain(0x1009);
        result.ObjectDictionary.OptionalObjects.Should().NotContain(0x100A);
    }

    [Fact]
    public void ReadString_SupportedObjectsCountExceedsAvailable_ParsesAvailable()
    {
        // Arrange – SupportedObjects says 5 but only 2 defined
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=5
1=0x1008
2=0x1009

[1008]
ParameterName=Object 1008
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0
PDOMapping=0

[1009]
ParameterName=Object 1009
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        // Parser loops over SupportedObjects range, gets what's available
        result.ObjectDictionary.OptionalObjects.Should().Contain(0x1008);
        result.ObjectDictionary.OptionalObjects.Should().Contain(0x1009);
    }

    [Fact]
    public void ReadString_OrphanObjectLinksSection_PreservedInAdditionalSections()
    {
        // Arrange – ObjectLinks section for object that doesn't exist in any list
        var content = BuildMinimalDcf(extraSections: @"
[9999ObjectLinks]
ObjectLinks=2
1=0x1000
2=0x2000
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        // Orphan ObjectLinks sections are preserved in AdditionalSections
        // since IsKnownSection no longer marks them as known
        result.AdditionalSections.Should().ContainKey("9999ObjectLinks");
        result.AdditionalSections["9999ObjectLinks"]["ObjectLinks"].Should().Be("2");
        result.AdditionalSections["9999ObjectLinks"]["1"].Should().Be("0x1000");
    }

    [Fact]
    public void ReadString_ObjectLinksForExistingObject_InAdditionalSections()
    {
        // Arrange – ObjectLinks section for an object that DOES exist
        // ObjectLinks for existing objects should be filtered from AdditionalSections.
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Custom Object
ObjectType=0x7
DataType=0x0007
AccessType=rw
DefaultValue=0
PDOMapping=0

[2000ObjectLinks]
ObjectLinks=1
1=0x1000
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        // ObjectLinks for existing object are parsed and attached to object
        var obj = result.ObjectDictionary.Objects[0x2000];
        obj.ObjectLinks.Should().Contain(0x1000);

        // ObjectLinks section should not be duplicated in AdditionalSections
        result.AdditionalSections.Should().NotContainKey("2000ObjectLinks");
    }

    [Fact]
    public void ReadString_NonHexObjectLinksPrefix_PreservedInAdditionalSections()
    {
        // Arrange — section name ending in "ObjectLinks" but with non-hex prefix
        var content = BuildMinimalDcf(extraSections: @"
[ZZZZObjectLinks]
ObjectLinks=1
1=0x1000
");

        // Act
        var result = _reader.ReadString(content);

        // Assert — not a valid hex index, so it should be in AdditionalSections
        result.AdditionalSections.Should().ContainKey("ZZZZObjectLinks");
    }

    [Fact]
    public void ReadString_EmptyPrefixObjectLinks_PreservedInAdditionalSections()
    {
        // Arrange — section named just "ObjectLinks" with no prefix
        var content = BuildMinimalDcf(extraSections: @"
[ObjectLinks]
ObjectLinks=1
1=0x1000
");

        // Act
        var result = _reader.ReadString(content);

        // Assert — empty prefix, preserved in AdditionalSections
        result.AdditionalSections.Should().ContainKey("ObjectLinks");
    }

    [Fact]
    public void ReadString_InvalidSubNumberCount_StillParsesAvailableSubObjects()
    {
        // Arrange – SubNumber says 3 but only 2 sub-sections exist
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x1018

[1018]
ParameterName=Record
SubNumber=3
ObjectType=0x9

[1018sub0]
ParameterName=Number of Entries
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=3
PDOMapping=0

[1018sub1]
ParameterName=Entry 1
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        // Parser creates sub-objects for index range 0..SubNumber
        var obj = result.ObjectDictionary.Objects[0x1018];
        obj.SubObjects.Should().HaveCount(2); // has sub0 and sub1 sections
    }

    [Fact]
    public void ReadString_NegativeOrZeroHexValue_ParsesCorrectly()
    {
        // Arrange – Negative hex (two's complement)
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Negative Value
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0xFFFFFFFF
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2000];
        obj.DefaultValue.Should().Be("0xFFFFFFFF");
    }

    [Fact]
    public void ReadString_OctalNotation_PreservedAndConvertsAsOctal()
    {
        // Arrange – Octal notation (0755 style); model stores the raw literal
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Octal Value
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0755
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert — raw string preserved; numeric conversion follows octal (#411 decision)
        var obj = result.ObjectDictionary.Objects[0x2000];
        obj.DefaultValue.Should().Be("0755");
        ValueConverter.ParseInteger(obj.DefaultValue).Should().Be(493u); // 7*64 + 5*8 + 5
    }

    [Fact]
    public void ReadString_PaddedLookingDefaultValue_ConvertsAsOctalNotDecimal()
    {
        // Arrange — "010" is a common padded-decimal pitfall; library treats it as octal 8
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2001

[2001]
ParameterName=Padded Looking Value
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=010
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2001];
        obj.DefaultValue.Should().Be("010");
        ValueConverter.ParseInteger(obj.DefaultValue).Should().Be(8u);
        ValueConverter.ParseInteger("0x10").Should().Be(16u);
        ValueConverter.ParseInteger("10").Should().Be(10u);
    }

    #endregion

    #region Object Type Tests

    [Fact]
    public void ReadString_ObjectType_Null_ParsedCorrectly()
    {
        // Arrange - ObjectType 0x0 (NULL)
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Null Object
ObjectType=0x0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2000];
        obj.ObjectType.Should().Be(0x0);
        obj.ParameterName.Should().Be("Null Object");
        obj.SubObjects.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_ObjectType_Domain_ParsedCorrectly()
    {
        // Arrange - ObjectType 0x2 (DOMAIN) with UploadFile/DownloadFile
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x1F50

[1F50]
ParameterName=Program Data
ObjectType=0x2
DataType=0x000F
AccessType=rw
PDOMapping=0
UploadFile=firmware.bin
DownloadFile=firmware.bin
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1F50];
        obj.ObjectType.Should().Be(0x2);
        obj.UploadFile.Should().Be("firmware.bin");
        obj.DownloadFile.Should().Be("firmware.bin");
        obj.SubObjects.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_ObjectType_DefType_ParsedCorrectly()
    {
        // Arrange - ObjectType 0x5 (DEFTYPE)
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x0005

[5]
ParameterName=UNSIGNED8
ObjectType=0x5
DataType=0x0005
AccessType=ro
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x0005];
        obj.ObjectType.Should().Be(0x5);
        obj.ParameterName.Should().Be("UNSIGNED8");
        obj.SubObjects.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_ObjectType_DefStruct_WithSubObjects_ParsedCorrectly()
    {
        // Arrange - ObjectType 0x6 (DEFSTRUCT) can have sub-objects
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x0020

[20]
ParameterName=PDO Communication Parameter
ObjectType=0x6
SubNumber=2

[20sub0]
ParameterName=Number of Entries
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=2
PDOMapping=0

[20sub1]
ParameterName=COB-ID
ObjectType=0x7
DataType=0x0007
AccessType=rw
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x0020];
        obj.ObjectType.Should().Be(0x6);
        obj.SubObjects.Should().HaveCount(2);
        obj.SubObjects[0].ParameterName.Should().Be("Number of Entries");
        obj.SubObjects[1].ParameterName.Should().Be("COB-ID");
    }

    [Fact]
    public void ReadString_ObjectType_DefStruct_WithoutSubNumber_ParsesSubObjects()
    {
        // Arrange - DEFSTRUCT (0x6) should trigger sub-object parsing even without SubNumber
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x0020

[20]
ParameterName=PDO Comm Param
ObjectType=0x6

[20sub0]
ParameterName=Number of Entries
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x0020];
        obj.ObjectType.Should().Be(0x6);
        obj.SubObjects.Should().ContainKey(0);
    }

    #endregion
}
