namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using AwesomeAssertions;
using Xunit;

public partial class EdsReaderTests
{
    #region Object Dictionary Parsing Tests

    [Fact]
    public void ReadString_ObjectDictionary_ParsesMandatoryObjects()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[DummyUsage]
Dummy0002=1

[MandatoryObjects]
SupportedObjects=2
1=0x1000
2=0x1001

[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x00000191
PDOMapping=0

[1001]
ParameterName=Error Register
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=1
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.MandatoryObjects.Should().HaveCount(2);
        result.ObjectDictionary.MandatoryObjects.Should().Contain(0x1000);
        result.ObjectDictionary.MandatoryObjects.Should().Contain(0x1001);
        result.ObjectDictionary.Objects.Should().ContainKey(0x1000);
        result.ObjectDictionary.Objects.Should().ContainKey(0x1001);
    }

    [Fact]
    public void ReadString_ObjectDictionary_ParsesOptionalObjects()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[DummyUsage]
Dummy0002=1

[MandatoryObjects]
SupportedObjects=0

[OptionalObjects]
SupportedObjects=2
1=0x1008
2=0x1009

[1008]
ParameterName=Manufacturer Device Name
ObjectType=0x7
DataType=0x0009
AccessType=ro
DefaultValue=Test Device
PDOMapping=0

[1009]
ParameterName=Manufacturer Hardware Version
ObjectType=0x7
DataType=0x0009
AccessType=ro
DefaultValue=1.0
PDOMapping=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.OptionalObjects.Should().HaveCount(2);
        result.ObjectDictionary.OptionalObjects.Should().Contain(0x1008);
        result.ObjectDictionary.OptionalObjects.Should().Contain(0x1009);
    }

    [Fact]
    public void ReadString_Object_ParsesAllProperties()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

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
DefaultValue=0x00000191
PDOMapping=0
LowLimit=0
HighLimit=0xFFFFFFFF
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1000];
        obj.Index.Should().Be(0x1000);
        obj.ParameterName.Should().Be("Device Type");
        obj.ObjectType.Should().Be(0x7);
        obj.DataType.Should().Be(0x0007);
        obj.AccessType.Should().Be(AccessType.ReadOnly);
        obj.DefaultValue.Should().Be("0x00000191");
        obj.PdoMapping.Should().BeFalse();
        obj.LowLimit.Should().Be("0");
        obj.HighLimit.Should().Be("0xFFFFFFFF");
    }

    [Fact]
    public void ReadString_ObjectWithSubObjects_ParsesCorrectly()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[DummyUsage]
Dummy0002=1
Dummy0005=1

[MandatoryObjects]
SupportedObjects=1
1=0x1018

[1018]
ParameterName=Identity Object
SubNumber=2
ObjectType=0x9

[1018sub0]
ParameterName=Number of Entries
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=2
PDOMapping=0

[1018sub1]
ParameterName=Vendor ID
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x00000100
PDOMapping=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1018];
        obj.SubNumber.Should().Be(2);
        obj.SubObjects.Should().ContainKey(0);
        obj.SubObjects.Should().ContainKey(1);
        obj.SubObjects[0].ParameterName.Should().Be("Number of Entries");
        obj.SubObjects[1].ParameterName.Should().Be("Vendor ID");
    }

    [Fact]
    public void ReadString_CompactSubObj_TemplateOnly_SynthesizesFromParentTemplate()
    {
        // Arrange — EDS compact storage without any [xxxsubN] sections
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=StatusBits
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=1
CompactSubObj=2
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.CompactSubObj.Should().Be(2);
        obj.SubObjects.Should().HaveCount(3);

        obj.SubObjects[0].ParameterName.Should().Be("NrOfObjects");
        obj.SubObjects[0].AccessType.Should().Be(AccessType.ReadOnly);
        obj.SubObjects[0].DataType.Should().Be(0x0005);
        obj.SubObjects[0].DefaultValue.Should().Be("2");
        obj.SubObjects[0].PdoMapping.Should().BeFalse();

        obj.SubObjects[1].ParameterName.Should().Be("StatusBits1");
        obj.SubObjects[1].ObjectType.Should().Be(0x7);
        obj.SubObjects[1].DataType.Should().Be(0x0005);
        obj.SubObjects[1].AccessType.Should().Be(AccessType.ReadOnly);
        obj.SubObjects[1].DefaultValue.Should().Be("0");
        obj.SubObjects[1].PdoMapping.Should().BeTrue();

        obj.SubObjects[2].ParameterName.Should().Be("StatusBits2");
    }

    [Fact]
    public void ReadString_CompactSubObj_NameSection_OverridesDefaultNames()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=StatusBits
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=0
CompactSubObj=3

[2100Name]
NrOfEntries=2
1=FirstBit
3=ThirdBit
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.SubObjects[0].ParameterName.Should().Be("NrOfObjects");
        obj.SubObjects[1].ParameterName.Should().Be("FirstBit");
        obj.SubObjects[2].ParameterName.Should().Be("StatusBits2"); // default rule
        obj.SubObjects[3].ParameterName.Should().Be("ThirdBit");
        result.AdditionalSections.Should().NotContainKey("2100Name");
    }

    [Fact]
    public void ReadString_OrphanNameSection_PreservedInAdditionalSections()
    {
        // Arrange — [xxxxName] without a matching CompactSubObj object must round-trip
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x1000

[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0
PDOMapping=0

[2100Name]
NrOfEntries=1
1=OrphanName
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().ContainKey("2100Name");
        result.AdditionalSections["2100Name"]["1"].Should().Be("OrphanName");
    }

    [Fact]
    public void ReadString_NameSectionWithoutCompactSubObj_PreservedInAdditionalSections()
    {
        // Arrange — object exists but CompactSubObj is absent/zero
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=StatusBits
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=0
SubNumber=1

[2100sub0]
ParameterName=Number of Entries
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[2100Name]
NrOfEntries=1
1=ShouldStayAdditional
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().ContainKey("2100Name");
        result.ObjectDictionary.Objects[0x2100].SubObjects.Should().NotContainKey(1);
    }

    [Fact]
    public void ReadString_NameSectionWithCompactSubObjZero_PreservedInAdditionalSections()
    {
        // Arrange — CompactSubObj=0 means no compact storage, so [2100Name] is not consumed
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=StatusBits
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=0
CompactSubObj=0

[2100Name]
NrOfEntries=1
1=ShouldStayAdditional
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.CompactSubObj.Should().Be(0);
        obj.SubObjects.Should().BeEmpty();
        result.AdditionalSections.Should().ContainKey("2100Name");
        result.AdditionalSections["2100Name"]["1"].Should().Be("ShouldStayAdditional");
    }

    [Fact]
    public void ReadString_CompactSubObj_NameSection_IgnoresKeysOutsideCompactListRange()
    {
        // Arrange — compact name lists cover sub-indexes 1..254 only; everything else is ignored:
        // sub-index 0 and 255 are out of range, non-numeric keys are not sub-indexes, empty values
        // carry no name, and keys without a matching sub-object have nothing to rename.
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=StatusBits
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=0
CompactSubObj=2

[2100Name]
NrOfEntries=2
0=NotTheEntryCount
255=ReservedSubIndex
Comment=NotASubIndex
1=FirstBit
2=
9=NoSuchSubObject
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.SubObjects.Should().HaveCount(3); // 0..2 — no sub-object is created by a name entry
        obj.SubObjects[0].ParameterName.Should().Be("NrOfObjects"); // key 0 ignored
        obj.SubObjects[1].ParameterName.Should().Be("FirstBit");
        obj.SubObjects[2].ParameterName.Should().Be("StatusBits2"); // empty value ignored
        obj.SubObjects.Should().NotContainKey(9);
        obj.SubObjects.Should().NotContainKey(255);
        result.AdditionalSections.Should().NotContainKey("2100Name");
    }

    [Fact]
    public void ReadString_CompactSubObj_NameSectionWithoutNrOfEntries_AppliesNames()
    {
        // Arrange — NrOfEntries is optional: the keys themselves define the list
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=StatusBits
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=0
CompactSubObj=2

[2100Name]
1=FirstBit
2=SecondBit
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.SubObjects[1].ParameterName.Should().Be("FirstBit");
        obj.SubObjects[2].ParameterName.Should().Be("SecondBit");
    }

    [Fact]
    public void ReadString_SectionEndingInNameWithNonHexPrefix_PreservedInAdditionalSections()
    {
        // Arrange — only a hex object index before "Name" makes a compact name section
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=StatusBits
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=0
CompactSubObj=2

[CustomName]
1=NotACompactList
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().ContainKey("CustomName");
        result.AdditionalSections["CustomName"]["1"].Should().Be("NotACompactList");
    }

    [Fact]
    public void ReadString_CompactSubObj_SampleDevice_SynthesizesMissingArrayElements()
    {
        // Arrange — sample_device.eds uses CompactSubObj with only sub0 expanded
        var result = _reader.ReadFile("Fixtures/sample_device.eds");

        // Assert
        var digitalIn = result.ObjectDictionary.Objects[0x6000];
        digitalIn.CompactSubObj.Should().Be(16);
        digitalIn.SubObjects.Should().HaveCount(17); // 0..16
        digitalIn.SubObjects[0].ParameterName.Should().Be("Number of Elements"); // explicit
        digitalIn.SubObjects[1].ParameterName.Should().Be("Digital Input 8-Bit1");
        digitalIn.SubObjects[1].AccessType.Should().Be(AccessType.ReadOnly);
        digitalIn.SubObjects[1].PdoMapping.Should().BeTrue();
        digitalIn.SubObjects[16].ParameterName.Should().Be("Digital Input 8-Bit16");

        var rpdoMap = result.ObjectDictionary.Objects[0x1600];
        rpdoMap.CompactSubObj.Should().Be(8);
        rpdoMap.SubObjects.Should().HaveCount(9); // 0..8
        rpdoMap.SubObjects[0].ParameterName.Should().Be("Number of Entries");
        rpdoMap.SubObjects[1].ParameterName.Should().Be("RPDO Mapping Parameter1");
    }

    [Fact]
    public void ReadString_AccessTypes_ParsesAllTypes()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[DummyUsage]
Dummy0002=1
Dummy0003=1
Dummy0004=1
Dummy0005=1
Dummy0006=1
Dummy0007=1

[MandatoryObjects]
SupportedObjects=6
1=0x2000
2=0x2001
3=0x2002
4=0x2003
5=0x2004
6=0x2005

[2000]
ParameterName=ReadOnly Object
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=0

[2001]
ParameterName=WriteOnly Object
ObjectType=0x7
DataType=0x0005
AccessType=wo
DefaultValue=0
PDOMapping=0

[2002]
ParameterName=ReadWrite Object
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0

[2003]
ParameterName=ReadWriteInput Object
ObjectType=0x7
DataType=0x0005
AccessType=rwr
DefaultValue=0
PDOMapping=0

[2004]
ParameterName=ReadWriteOutput Object
ObjectType=0x7
DataType=0x0005
AccessType=rww
DefaultValue=0
PDOMapping=0

[2005]
ParameterName=Constant Object
ObjectType=0x7
DataType=0x0005
AccessType=const
DefaultValue=0
PDOMapping=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.Objects[0x2000].AccessType.Should().Be(AccessType.ReadOnly);
        result.ObjectDictionary.Objects[0x2001].AccessType.Should().Be(AccessType.WriteOnly);
        result.ObjectDictionary.Objects[0x2002].AccessType.Should().Be(AccessType.ReadWrite);
        result.ObjectDictionary.Objects[0x2003].AccessType.Should().Be(AccessType.ReadWriteInput);
        result.ObjectDictionary.Objects[0x2004].AccessType.Should().Be(AccessType.ReadWriteOutput);
        result.ObjectDictionary.Objects[0x2005].AccessType.Should().Be(AccessType.Constant);
    }

    [Fact]
    public void ReadString_Object_SafetyProperties_ParsedCorrectly()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[OptionalObjects]
SupportedObjects=1
1=0x6100

[6100]
ParameterName=SRDO Input
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=1
SRDOMapping=1
InvertedSRAD=0x610101
SubNumber=1

[6100sub0]
ParameterName=Number of Entries
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0
SRDOMapping=0

[6100sub1]
ParameterName=SRDO Input 1
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=1
SRDOMapping=1
InvertedSRAD=0x610101
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x6100];
        obj.SrdoMapping.Should().BeTrue();
        obj.InvertedSrad.Should().Be("0x610101");

        obj.SubObjects[0].SrdoMapping.Should().BeFalse();
        obj.SubObjects[0].InvertedSrad.Should().BeNullOrEmpty();

        obj.SubObjects[1].SrdoMapping.Should().BeTrue();
        obj.SubObjects[1].InvertedSrad.Should().Be("0x610101");
    }

    [Fact]
    public void ReadString_Object_SafetyProperties_DefaultValues()
    {
        // Arrange
        var content = @"
[DeviceInfo]
VendorName=Test

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
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1000];
        obj.SrdoMapping.Should().BeFalse();
        obj.InvertedSrad.Should().BeNullOrEmpty();
    }

    private static string EdsWithSubIndexes(ushort index, string subNumber, params byte[] subIndexes)
    {
        var hex = index.ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
        var sb = new System.Text.StringBuilder();
        sb.Append("[FileInfo]\nFileName=gap.eds\n[DeviceInfo]\nVendorName=V\n");
        sb.Append("[OptionalObjects]\nSupportedObjects=1\n1=0x").Append(hex).Append('\n');
        sb.Append('[').Append(hex).Append("]\nParameterName=List\nObjectType=0x8\nSubNumber=").Append(subNumber).Append('\n');
        foreach (var sub in subIndexes)
        {
            sb.Append('[').Append(hex).Append("sub").Append(sub.ToString("X", System.Globalization.CultureInfo.InvariantCulture)).Append("]\n")
              .Append("ParameterName=Sub").Append(sub).Append('\n')
              .Append("ObjectType=0x7\nDataType=0x0007\nAccessType=ro\nDefaultValue=0x1\n");
        }

        return sb.ToString();
    }

    [Fact]
    public void ReadString_SubIndexListWithGaps_ParsesSubIndexesAboveSubNumber()
    {
        // CiA 306-1 Figure 16: SubNumber=3 describes sub0, sub1 and sub4 (#563).
        var content = EdsWithSubIndexes(0x1010, "3", 0, 1, 4);

        var result = _reader.ReadString(content);

        var obj = result.ObjectDictionary.Objects[0x1010];
        obj.SubNumber.Should().Be(3);
        obj.SubObjects.Keys.Should().BeEquivalentTo(new byte[] { 0, 1, 4 });
    }

    [Fact]
    public void ReadString_SubIndexListWithGapToSubFF_AtMaxValue()
    {
        var content = EdsWithSubIndexes(0x2000, "3", 0, 1, 0xFF);

        var result = _reader.ReadString(content);

        result.ObjectDictionary.Objects[0x2000].SubObjects.Keys
            .Should().BeEquivalentTo(new byte[] { 0, 1, 0xFF });
    }

    [Fact]
    public void ReadString_SubIndexListWithGaps_ValidatedRoundTrip()
    {
        var content = EdsWithSubIndexes(0x1010, "3", 0, 1, 4);
        var model = _reader.ReadString(content);

        var written = CanOpenFile.Eds.WriteToString(model, CanOpenWriteOptions.Validated);
        var reloaded = _reader.ReadString(written);

        reloaded.ObjectDictionary.Objects[0x1010].SubObjects.Keys
            .Should().BeEquivalentTo(new byte[] { 0, 1, 4 });
    }

    #endregion
}
