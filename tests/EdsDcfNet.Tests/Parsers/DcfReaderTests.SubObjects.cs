namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Utilities;

public partial class DcfReaderTests
{
    #region SubObjects Tests

    [Fact]
    public void ReadString_SubObjects_ParsedViaSubNumber()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
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
DefaultValue=0x100
PDOMapping=0
ParameterValue=0x100
Denotation=VendorID

[1018sub2]
ParameterName=Product Code
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x1001
PDOMapping=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1018];
        obj.SubNumber.Should().Be(2);
        obj.SubObjects.Should().HaveCount(3); // sub0, sub1, sub2
        obj.SubObjects[0].ParameterName.Should().Be("Number of Entries");
        obj.SubObjects[1].ParameterName.Should().Be("Vendor ID");
        obj.SubObjects[1].ParameterValue.Should().Be("0x100");
        obj.SubObjects[1].Denotation.Should().Be("VendorID");
        obj.SubObjects[2].ParameterName.Should().Be("Product Code");
    }

    [Fact]
    public void ReadString_SubObjects_ParsedViaObjectType_Array()
    {
        // Arrange – ObjectType 0x8 (ARRAY) triggers sub-object parsing
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x6000

[6000]
ParameterName=Digital Input
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=1
SubNumber=2

[6000sub0]
ParameterName=Number of Elements
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=2
PDOMapping=0

[6000sub1]
ParameterName=DI Channel 1
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=1

[6000sub2]
ParameterName=DI Channel 2
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=1
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x6000];
        obj.ObjectType.Should().Be(0x8);
        obj.SubObjects.Should().HaveCount(3);
    }

    [Fact]
    public void ReadString_CompactSubObj_ValueSection_AppliesParameterValues()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=Config Array
ObjectType=0x8
DataType=0x0005
AccessType=rw
SubNumber=3
CompactSubObj=3

[2100sub0]
ParameterName=Number of Elements
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=3
PDOMapping=0

[2100sub1]
ParameterName=Element 1
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0

[2100sub2]
ParameterName=Element 2
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0

[2100sub3]
ParameterName=Element 3
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0

[2100Value]
NrOfEntries=3
1=10
2=20
3=30
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.CompactSubObj.Should().Be(3);
        obj.SubObjects[1].ParameterValue.Should().Be("10");
        obj.SubObjects[2].ParameterValue.Should().Be("20");
        obj.SubObjects[3].ParameterValue.Should().Be("30");
    }

    [Fact]
    public void ReadString_CompactSubObj_DenotationSection_AppliesDenotations()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=Config Array
ObjectType=0x8
DataType=0x0005
AccessType=rw
SubNumber=2
CompactSubObj=2

[2100sub0]
ParameterName=Number of Elements
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=2
PDOMapping=0

[2100sub1]
ParameterName=Element 1
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0

[2100sub2]
ParameterName=Element 2
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0

[2100Denotation]
NrOfEntries=2
1=Label A
2=Label B
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.SubObjects[1].Denotation.Should().Be("Label A");
        obj.SubObjects[2].Denotation.Should().Be("Label B");
    }

    [Fact]
    public void ReadString_CompactSubObj_TemplateOnly_SynthesizesSubObjectsAndAppliesValues()
    {
        // Arrange — CiA 306 compact form: parent template only, no [xxxsubN] sections
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=ConfigArray
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=1
CompactSubObj=3

[2100Value]
NrOfEntries=3
1=10
2=20
3=30
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.CompactSubObj.Should().Be(3);
        obj.SubObjects.Should().HaveCount(4); // 0..3

        var sub0 = obj.SubObjects[0];
        sub0.ParameterName.Should().Be("NrOfObjects");
        sub0.ObjectType.Should().Be(0x7);
        sub0.DataType.Should().Be(0x0005);
        sub0.AccessType.Should().Be(AccessType.ReadOnly);
        sub0.DefaultValue.Should().Be("3");
        sub0.PdoMapping.Should().BeFalse();
        sub0.ParameterValue.Should().BeNull();

        obj.SubObjects[1].ParameterName.Should().Be("ConfigArray1");
        obj.SubObjects[1].DataType.Should().Be(0x0005);
        obj.SubObjects[1].AccessType.Should().Be(AccessType.ReadWrite);
        obj.SubObjects[1].DefaultValue.Should().Be("0");
        obj.SubObjects[1].PdoMapping.Should().BeTrue();
        obj.SubObjects[1].ParameterValue.Should().Be("10");

        obj.SubObjects[2].ParameterValue.Should().Be("20");
        obj.SubObjects[3].ParameterValue.Should().Be("30");
        obj.SubObjects[3].ParameterName.Should().Be("ConfigArray3");
    }

    [Fact]
    public void ReadString_CompactSubObj_TemplateOnly_AppliesDenotations()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=ConfigArray
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0
CompactSubObj=2

[2100Denotation]
NrOfEntries=2
1=Label A
2=Label B
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.SubObjects.Should().HaveCount(3);
        obj.SubObjects[1].Denotation.Should().Be("Label A");
        obj.SubObjects[2].Denotation.Should().Be("Label B");
        obj.SubObjects[0].Denotation.Should().BeNull();
    }

    [Fact]
    public void ReadString_CompactSubObj_TemplateOnly_PartialExpandedSub0_PreservesExplicitAndSynthesizesRest()
    {
        // Arrange — hybrid: explicit sub0, synthesize 1..N from template
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=DigitalInput
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=1
CompactSubObj=2

[2100sub0]
ParameterName=Number of Elements
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=2
PDOMapping=0

[2100Value]
NrOfEntries=2
1=0xAA
2=0xBB
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.SubObjects.Should().HaveCount(3);
        obj.SubObjects[0].ParameterName.Should().Be("Number of Elements");
        obj.SubObjects[0].DefaultValue.Should().Be("2");
        obj.SubObjects[1].ParameterName.Should().Be("DigitalInput1");
        obj.SubObjects[1].ParameterValue.Should().Be("0xAA");
        obj.SubObjects[2].ParameterValue.Should().Be("0xBB");
    }

    [Fact]
    public void ReadString_CompactSubObj_AtMaxValue_SynthesizesAllSubIndexes()
    {
        // Arrange — CompactSubObj = 254 (max listable value sub-index per CiA 306)
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=BigArray
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0
CompactSubObj=254

[2100Value]
NrOfEntries=2
1=1
254=254
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.CompactSubObj.Should().Be(254);
        obj.SubObjects.Should().HaveCount(255); // 0..254
        obj.SubObjects[0].DefaultValue.Should().Be("254");
        obj.SubObjects[1].ParameterValue.Should().Be("1");
        obj.SubObjects[254].ParameterValue.Should().Be("254");
        obj.SubObjects[254].ParameterName.Should().Be("BigArray254");
        obj.SubObjects[100].ParameterValue.Should().BeNull();
        obj.SubObjects[100].DefaultValue.Should().Be("0");
    }

    [Fact]
    public void ReadString_CompactSubObj_255_DoesNotSynthesizeSubIndexFF()
    {
        // Arrange — CompactSubObj=255 must not fabricate SubObjects[0xFF]
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=BigArray
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0
CompactSubObj=255
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.CompactSubObj.Should().Be(255);
        obj.SubObjects.Should().HaveCount(255); // 0..254 only
        obj.SubObjects.Should().ContainKey(0);
        obj.SubObjects.Should().ContainKey(254);
        obj.SubObjects.Should().NotContainKey(255);
        obj.SubObjects[0].DefaultValue.Should().Be("255");
    }

    [Fact]
    public void ReadString_CompactSubObj_255_ParsesExplicitSubFFWithoutSubNumber()
    {
        // Arrange — CompactSubObj=255 without SubNumber: 0xFF is not synthesized, but an
        // explicit [2100subFF] section must still be parsed instead of silently dropped.
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=BigArray
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0
CompactSubObj=255

[2100subFF]
ParameterName=Identity
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0
PDOMapping=0
ParameterValue=keep-me
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.SubObjects.Should().HaveCount(256); // 0..254 synthesized + explicit 0xFF
        obj.SubObjects[255].ParameterName.Should().Be("Identity");
        obj.SubObjects[255].DataType.Should().Be(0x0007);
        obj.SubObjects[255].ParameterValue.Should().Be("keep-me");
        obj.SubObjects[254].ParameterName.Should().Be("BigArray254"); // still synthesized
        result.AdditionalSections.Should().NotContainKey("2100subFF");
    }

    [Fact]
    public void ReadString_CompactSubObj_ValueKey255_DoesNotApplyToSubFF()
    {
        // Arrange — compact lists are 1..254; key 255 must not touch SubObjects[0xFF]
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=BigArray
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0
SubNumber=255
CompactSubObj=2

[2100subFF]
ParameterName=Identity
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0
PDOMapping=0
ParameterValue=keep-me

[2100Value]
NrOfEntries=2
1=10
255=should-not-apply
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.SubObjects[1].ParameterValue.Should().Be("10");
        obj.SubObjects[255].ParameterValue.Should().Be("keep-me");
    }

    [Fact]
    public void ReadString_CompactSubObj_MalformedNrOfEntries_AppliesValueLeniently()
    {
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=ConfigArray
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0
CompactSubObj=1

[2100Value]
NrOfEntries=oops
1=10
");

        // A malformed count is reported (strict: thrown, see ModuleObjectKeyMatrixTests); it is
        // not a loop bound, so the value is still applied.
        var result = _reader.ReadString(content);

        result.ObjectDictionary.Objects[0x2100].SubObjects[1].ParameterValue.Should().Be("10");
    }

    [Fact]
    public void ReadString_CompactSubObj_ValueSection_IgnoresKeysOutsideCompactListRange()
    {
        // Arrange — compact value lists cover sub-indexes 1..254 only; sub-index 0, non-numeric
        // keys, empty values and keys without a matching sub-object must all be ignored.
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=ConfigArray
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0
CompactSubObj=2

[2100Value]
NrOfEntries=2
0=NotTheEntryCount
Comment=NotASubIndex
1=10
2=
9=99
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.SubObjects.Should().HaveCount(3); // 0..2 — no sub-object is created by a value entry
        obj.SubObjects[0].ParameterValue.Should().BeNull(); // key 0 ignored
        obj.SubObjects[1].ParameterValue.Should().Be("10");
        obj.SubObjects[2].ParameterValue.Should().BeNull(); // empty value ignored
        obj.SubObjects.Should().NotContainKey(9);
    }

    [Fact]
    public void ReadString_CompactSubObj_NameSection_OverridesNamesAndIsConsumed()
    {
        // Arrange — [xxxxName] is shared EDS/DCF compact storage (CiA 306 §4.5.2.4.2)
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=ConfigArray
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0
CompactSubObj=2

[2100Name]
NrOfEntries=1
2=SecondEntry

[2100Value]
NrOfEntries=1
2=20
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2100];
        obj.SubObjects[1].ParameterName.Should().Be("ConfigArray1");
        obj.SubObjects[2].ParameterName.Should().Be("SecondEntry");
        obj.SubObjects[2].ParameterValue.Should().Be("20");
        result.AdditionalSections.Should().NotContainKey("2100Name");
    }

    [Fact]
    public void ReadString_OrphanNameSection_PreservedInAdditionalSections()
    {
        // Arrange — [xxxxName] without a matching CompactSubObj object must round-trip
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2100

[2100]
ParameterName=ConfigArray
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0
CompactSubObj=2

[2200Name]
NrOfEntries=1
1=OrphanName
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().ContainKey("2200Name");
        result.AdditionalSections["2200Name"]["1"].Should().Be("OrphanName");
    }

    [Fact]
    public void ReadString_SubObject_AllFieldsParsed()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[OptionalObjects]
SupportedObjects=1
1=0x1018

[1018]
ParameterName=Record
SubNumber=1
ObjectType=0x9

[1018sub0]
ParameterName=Sub Zero
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=42
LowLimit=0
HighLimit=255
PDOMapping=1
ParameterValue=99
Denotation=SubDenotation
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var sub = result.ObjectDictionary.Objects[0x1018].SubObjects[0];
        sub.SubIndex.Should().Be(0);
        sub.ParameterName.Should().Be("Sub Zero");
        sub.ObjectType.Should().Be(0x7);
        sub.DataType.Should().Be(0x0005);
        sub.AccessType.Should().Be(AccessType.ReadWrite);
        sub.DefaultValue.Should().Be("42");
        sub.LowLimit.Should().Be("0");
        sub.HighLimit.Should().Be("255");
        sub.PdoMapping.Should().BeTrue();
        sub.ParameterValue.Should().Be("99");
        sub.Denotation.Should().Be("SubDenotation");
    }

    [Fact]
    public void ReadString_DeviceInfo_CANopenSafetySupported_ParsedCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf().Replace(
            "LSS_Supported=0",
            "LSS_Supported=0\nCANopenSafetySupported=1");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DeviceInfo.CANopenSafetySupported.Should().BeTrue();
    }

    [Fact]
    public void ReadString_DeviceInfo_CANopenSafetySupported_DefaultsFalse()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DeviceInfo.CANopenSafetySupported.Should().BeFalse();
    }

    [Fact]
    public void ReadString_Object_SafetyProperties_ParsedCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
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
");

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
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x1000];
        obj.SrdoMapping.Should().BeFalse();
        obj.InvertedSrad.Should().BeNullOrEmpty();
    }

    #endregion
}
