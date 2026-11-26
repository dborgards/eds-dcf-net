namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using AwesomeAssertions;
using Xunit;

public partial class EdsReaderTests
{
    #region Vendor Section Preservation Tests

    [Fact]
    public void ReadString_SectionContainingSub_NotSubObject_PreservedInAdditionalSections()
    {
        // Arrange - "SubSystem" contains "sub" but is NOT a sub-object section (no hex prefix)
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

[SubSystem]
VendorKey=VendorData
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - SubSystem should NOT be swallowed as a known section
        result.AdditionalSections.Should().ContainKey("SubSystem");
        result.AdditionalSections["SubSystem"]["VendorKey"].Should().Be("VendorData");
    }

    [Fact]
    public void ReadString_HexPrefixedSubExtra_NotSubObject_PreservedInAdditionalSections()
    {
        // Arrange - "[1000subExtra]" has a hex prefix + "sub" but the suffix is not a hex sub-index
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

[1000sub0]
ParameterName=Highest sub-index
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0x00
PDOMapping=0

[1000subExtra]
VendorKey=VendorData
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - custom hex-prefixed "sub*" names must round-trip via AdditionalSections
        result.AdditionalSections.Should().ContainKey("1000subExtra");
        result.AdditionalSections["1000subExtra"]["VendorKey"].Should().Be("VendorData");
        // [1000sub0] of a VAR without SubNumber is not loaded as a sub-object; it is kept
        // unchanged instead of being dropped (INI_ORPHAN_SUB_OBJECT_SECTION).
        result.AdditionalSections["1000sub0"]["ParameterName"].Should().Be("Highest sub-index");
        result.ObjectDictionary.Objects.Should().ContainKey((ushort)0x1000);
        result.ObjectDictionary.Objects[0x1000].SubObjects.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_HexPrefixedSubEmptySuffix_NotSubObject_PreservedInAdditionalSections()
    {
        // Arrange - "[1000sub]" ends with "sub" and has no hex sub-index suffix
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

[1000sub]
VendorKey=EmptySuffix
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - empty suffix after "sub" must not be treated as a known sub-object
        result.AdditionalSections.Should().ContainKey("1000sub");
        result.AdditionalSections["1000sub"]["VendorKey"].Should().Be("EmptySuffix");
    }

    [Fact]
    public void ReadString_HexPrefixedSubWithWhitespace_NotSubObject_PreservedInAdditionalSections()
    {
        // Arrange - HexNumber would accept " 1", but ParseSubObject only looks up "1000sub1"
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

[1000sub 1]
VendorKey=SpacedSuffix
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - must round-trip via AdditionalSections, not be swallowed as known
        result.AdditionalSections.Should().ContainKey("1000sub 1");
        result.AdditionalSections["1000sub 1"]["VendorKey"].Should().Be("SpacedSuffix");
    }

    [Fact]
    public void ReadString_HexPrefixedSubIndexOverflow_NotSubObject_PreservedInAdditionalSections()
    {
        // Arrange - "10000" is hex digits but does not fit in ushort (covers TryParse false branch)
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

[10000sub0]
VendorKey=OverflowPrefix
";

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().ContainKey("10000sub0");
        result.AdditionalSections["10000sub0"]["VendorKey"].Should().Be("OverflowPrefix");
    }

    [Fact]
    public void ReadString_SectionStartingWithM_NotModule_PreservedInAdditionalSections()
    {
        // Arrange - "Manufacturing" starts with "M" but is NOT a module section
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

[Manufacturing]
SerialFormat=12345
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - Manufacturing should NOT be swallowed as a known section
        result.AdditionalSections.Should().ContainKey("Manufacturing");
        result.AdditionalSections["Manufacturing"]["SerialFormat"].Should().Be("12345");
    }

    [Fact]
    public void ReadString_ValidSubObjectSection_StillRecognized()
    {
        // Arrange - "1018sub1" is a valid sub-object section (hex prefix + "sub" + hex suffix)
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x1018

[1018]
ParameterName=Identity
ObjectType=0x9
SubNumber=2

[1018sub0]
ParameterName=Number of Entries
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[1018sub1]
ParameterName=Vendor ID
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x100
PDOMapping=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - valid sub-object sections should still be parsed, not in AdditionalSections
        result.ObjectDictionary.Objects[0x1018].SubObjects.Should().ContainKey(1);
        result.AdditionalSections.Should().NotContainKey("1018sub1");
    }

    [Fact]
    public void ReadString_ModuleSectionWithSubExtendSuffix_PreservedInAdditionalSections()
    {
        // Arrange - "M1SubExtend1" is no section the module parser loads (it reads
        // [MxSubExtends] and [MxSubExtxxxx] with a hexadecimal index), so it is kept.
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[SupportedModules]
NrOfEntries=1

[M1ModuleInfo]
ProductName=Module
ProductVersion=1
ProductRevision=0
OrderCode=MOD-1

[M1SubExtend1]
SomeKey=SomeValue
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - M1SubExtend1 is an ordinary additional section, not dropped as known
        result.AdditionalSections["M1SubExtend1"]["SomeKey"].Should().Be("SomeValue");
    }

    [Fact]
    public void ReadString_ModuleSectionWithSubExtSuffix_RecognizedAsKnown()
    {
        // Arrange - "M1SubExt1" has suffix "SubExt1" which StartsWith("SubExt") but NOT "SubExtend"
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[SupportedModules]
NrOfEntries=1

[M1ModuleInfo]
ProductName=Module
ProductVersion=1
ProductRevision=0
OrderCode=MOD-1

[M1SubExt1]
SomeKey=SomeValue
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - M1SubExt1 is a known module section, NOT in AdditionalSections
        result.AdditionalSections.Should().NotContainKey("M1SubExt1");
    }

    [Fact]
    public void ReadString_ModuleSectionWithCommentsSuffix_RecognizedAsKnown()
    {
        // Arrange - "M1Comments" has suffix "Comments"
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[SupportedModules]
NrOfEntries=1

[M1ModuleInfo]
ProductName=Module
ProductVersion=1
ProductRevision=0
OrderCode=MOD-1

[M1Comments]
Lines=1
Line1=Module comment
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - M1Comments is a known module section, NOT in AdditionalSections
        result.AdditionalSections.Should().NotContainKey("M1Comments");
    }

    [Fact]
    public void ReadString_ModuleSectionWithUnknownSuffix_PreservedInAdditionalSections()
    {
        // Arrange - a module-style section name with an unrecognised suffix ("M1UnknownSuffix")
        // is not a known module section and should be preserved in AdditionalSections.
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[M1UnknownSuffix]
Key=Value
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - M1UnknownSuffix is NOT a known module section, so it lands in AdditionalSections
        result.AdditionalSections.Should().ContainKey("M1UnknownSuffix");
    }

    [Fact]
    public void ReadString_DummyUsage_NonDummyKey_IsIgnored()
    {
        // Arrange - DummyUsage section contains a key that does not follow the DummyXXXX
        // naming convention; such keys are ignored and not added to DummyUsage.
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[DummyUsage]
Dummy0002=1
SupportedObjects=0
";

        // Act
        var result = _reader.ReadString(content);

        // Assert - only the valid DummyXXXX entry is in DummyUsage; the other key is ignored
        result.ObjectDictionary.DummyUsage.Should().ContainKey(0x0002);
        result.ObjectDictionary.DummyUsage.Should().HaveCount(1);
    }

    [Fact]
    public void ReadString_DummyUsage_InvalidHexKey_StrictParsing_ThrowsEdsParseException()
    {
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[DummyUsage]
DummyZZZZ=1
";

        var act = () => CanOpenFile.Eds.ReadString(content, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*DummyUsage*DummyZZZZ*");
    }

    [Fact]
    public void ReadString_DummyUsage_InvalidHexKey_Lenient_IsSkipped()
    {
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[DummyUsage]
Dummy0002=1
DummyZZZZ=1
";

        var result = CanOpenFile.Eds.ReadString(content);

        result.ObjectDictionary.DummyUsage.Should().ContainKey(0x0002);
        result.ObjectDictionary.DummyUsage.Should().HaveCount(1);
    }

    [Fact]
    public void ReadString_DummyUsage_BareDummyKey_StrictParsing_ThrowsEdsParseException()
    {
        // "Dummy" with no hex suffix (length == 5) must take the empty-index path and reject in strict mode.
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[DummyUsage]
Dummy=1
";

        var act = () => CanOpenFile.Eds.ReadString(content, new CanOpenFileOptions { StrictParsing = true });

        act.Should().Throw<EdsParseException>()
            .WithMessage("*DummyUsage*Dummy*");
    }

    [Fact]
    public void ReadString_DummyUsage_BareDummyKey_Lenient_IsSkipped()
    {
        var content = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=0

[DummyUsage]
Dummy0002=1
Dummy=1
";

        var result = CanOpenFile.Eds.ReadString(content);

        result.ObjectDictionary.DummyUsage.Should().ContainKey(0x0002);
        result.ObjectDictionary.DummyUsage.Should().HaveCount(1);
    }

    #endregion
}
