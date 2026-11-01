namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;
using EdsDcfNet.Utilities;

public partial class DcfReaderTests
{
    #region DummyUsage Tests

    [Fact]
    public void ReadString_DummyUsage_ParsesHexKeysAndBoolValues()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[DummyUsage]
Dummy0002=1
Dummy0003=0
Dummy0005=1
Dummy0007=1
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.DummyUsage.Should().ContainKey(2);
        result.ObjectDictionary.DummyUsage[2].Should().BeTrue();
        result.ObjectDictionary.DummyUsage[3].Should().BeFalse();
        result.ObjectDictionary.DummyUsage[5].Should().BeTrue();
        result.ObjectDictionary.DummyUsage[7].Should().BeTrue();
    }

    [Fact]
    public void ReadString_NoDummyUsage_EmptyDictionary()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ObjectDictionary.DummyUsage.Should().BeEmpty();
    }

    #endregion

    #region ObjectLinks Tests

    [Fact]
    public void ReadString_ObjectLinks_ParsedCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Linked Object
ObjectType=0x7
DataType=0x0007
AccessType=rw
DefaultValue=0
PDOMapping=0

[2000ObjectLinks]
ObjectLinks=2
1=0x2100
2=0x1000
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        var obj = result.ObjectDictionary.Objects[0x2000];
        obj.ObjectLinks.Should().HaveCount(2);
        obj.ObjectLinks.Should().Contain((ushort)0x2100);
        obj.ObjectLinks.Should().Contain((ushort)0x1000);
    }

    #endregion

    #region Comments Tests

    [Fact]
    public void ReadString_Comments_ParsedCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[Comments]
Lines=3
Line1=First line
Line2=Second line
Line3=Third line
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Comments.Should().NotBeNull();
        result.Comments!.Lines.Should().Be(3);
        result.Comments.CommentLines.Should().HaveCount(3);
        result.Comments.CommentLines[1].Should().Be("First line");
        result.Comments.CommentLines[2].Should().Be("Second line");
        result.Comments.CommentLines[3].Should().Be("Third line");
    }

    [Fact]
    public void ReadString_NoComments_ReturnsNull()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Comments.Should().BeNull();
    }

    #endregion

    #region SupportedModules Tests

    [Fact]
    public void ReadString_SupportedModules_ParsedCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[SupportedModules]
NrOfEntries=2

[M1ModuleInfo]
ProductName=Input Module
ProductVersion=1
ProductRevision=0
OrderCode=MOD-IN-8

[M1FixedObjects]
NrOfEntries=1
1=0x6000

[M2ModuleInfo]
ProductName=Output Module
ProductVersion=2
ProductRevision=1
OrderCode=MOD-OUT-8
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.SupportedModules.Should().HaveCount(2);

        var m1 = result.SupportedModules[0];
        m1.ModuleNumber.Should().Be(1);
        m1.ProductName.Should().Be("Input Module");
        m1.ProductVersion.Should().Be(1);
        m1.ProductRevision.Should().Be(0);
        m1.OrderCode.Should().Be("MOD-IN-8");
        m1.FixedObjects.Should().Contain((ushort)0x6000);

        var m2 = result.SupportedModules[1];
        m2.ModuleNumber.Should().Be(2);
        m2.ProductName.Should().Be("Output Module");
        m2.ProductVersion.Should().Be(2);
        m2.ProductRevision.Should().Be(1);
        m2.OrderCode.Should().Be("MOD-OUT-8");
        m2.FixedObjects.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_NoSupportedModules_EmptyList()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.SupportedModules.Should().BeEmpty();
    }

    #endregion

    #region ConnectedModules Tests

    [Fact]
    public void ReadString_ConnectedModules_ParsedCorrectly()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[ConnectedModules]
NrOfEntries=3
1=1
2=2
3=1
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ConnectedModules.Should().HaveCount(3);
        result.ConnectedModules.Should().ContainInOrder(1, 2, 1);
    }

    [Fact]
    public void ReadString_ConnectedModules_InvalidEntries_AreSkipped()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[ConnectedModules]
NrOfEntries=4
1=1
2=abc
3=
4=2
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ConnectedModules.Should().ContainInOrder(1, 2);
    }

    [Fact]
    public void ReadString_NoConnectedModules_EmptyList()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.ConnectedModules.Should().BeEmpty();
    }

    #endregion

    #region AdditionalSections Tests

    [Fact]
    public void ReadString_UnknownSection_PreservedInAdditionalSections()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[VendorSpecific]
CustomKey=CustomValue
Version=1.0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().ContainKey("VendorSpecific");
        result.AdditionalSections["VendorSpecific"]["CustomKey"].Should().Be("CustomValue");
        result.AdditionalSections["VendorSpecific"]["Version"].Should().Be("1.0");
    }

    [Fact]
    public void ReadString_AdditionalSections_AreCaseInsensitiveAndResolveCaseCollisions()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[VendorSpecific]
CustomKey=Initial
Another=Value

[vendorspecific]
customkey=Overridden
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().ContainKey("VENDORSPECIFIC");
        result.AdditionalSections["vendorspecific"]["CUSTOMKEY"].Should().Be("Overridden");
        result.AdditionalSections["Vendorspecific"]["another"].Should().Be("Value");
    }

    [Fact]
    public void ReadString_KnownSections_NotInAdditionalSections()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().NotContainKey("FileInfo");
        result.AdditionalSections.Should().NotContainKey("DeviceInfo");
        result.AdditionalSections.Should().NotContainKey("DeviceCommissioning");
        result.AdditionalSections.Should().NotContainKey("MandatoryObjects");
        result.AdditionalSections.Should().NotContainKey("1000");
    }

    [Fact]
    public void ReadString_SectionContainingSub_NotSubObject_PreservedInAdditionalSections()
    {
        // Arrange - "SubSystem" contains "sub" but is NOT a sub-object section (no hex prefix)
        var content = BuildMinimalDcf(extraSections: @"
[SubSystem]
VendorKey=VendorData
");

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
        var content = BuildMinimalDcf(extraSections: @"
[1000subExtra]
VendorKey=VendorData
");

        // Act
        var result = _reader.ReadString(content);

        // Assert - custom hex-prefixed "sub*" names must round-trip via AdditionalSections
        result.AdditionalSections.Should().ContainKey("1000subExtra");
        result.AdditionalSections["1000subExtra"]["VendorKey"].Should().Be("VendorData");
    }

    [Fact]
    public void ReadString_HexPrefixedSubEmptySuffix_NotSubObject_PreservedInAdditionalSections()
    {
        // Arrange - "[1000sub]" has a hex prefix + "sub" but an empty suffix
        var content = BuildMinimalDcf(extraSections: @"
[1000sub]
VendorKey=EmptySuffix
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().ContainKey("1000sub");
        result.AdditionalSections["1000sub"]["VendorKey"].Should().Be("EmptySuffix");
    }

    [Fact]
    public void ReadString_HexPrefixedSubWithWhitespace_NotSubObject_PreservedInAdditionalSections()
    {
        // Arrange - HexNumber would accept " 1", but ParseSubObject only looks up "1000sub1"
        var content = BuildMinimalDcf(extraSections: @"
[1000sub 1]
VendorKey=SpacedSuffix
");

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
        var content = BuildMinimalDcf(extraSections: @"
[10000sub0]
VendorKey=OverflowPrefix
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().ContainKey("10000sub0");
        result.AdditionalSections["10000sub0"]["VendorKey"].Should().Be("OverflowPrefix");
    }

    [Fact]
    public void ReadString_SectionStartingWithM_NotModule_PreservedInAdditionalSections()
    {
        // Arrange - "Manufacturing" starts with "M" but is NOT a module section (no digit after "M")
        var content = BuildMinimalDcf(extraSections: @"
[Manufacturing]
SerialFormat=12345
");

        // Act
        var result = _reader.ReadString(content);

        // Assert - Manufacturing should NOT be swallowed as a known section
        result.AdditionalSections.Should().ContainKey("Manufacturing");
        result.AdditionalSections["Manufacturing"]["SerialFormat"].Should().Be("12345");
    }

    [Fact]
    public void ReadString_SectionEndingWithValue_NotHexPrefixed_PreservedInAdditionalSections()
    {
        // Arrange - "SomeValue" ends with "Value" but prefix is NOT a valid hex index
        var content = BuildMinimalDcf(extraSections: @"
[SomeValue]
Data=123
");

        // Act
        var result = _reader.ReadString(content);

        // Assert - SomeValue should NOT be swallowed as a known section
        result.AdditionalSections.Should().ContainKey("SomeValue");
        result.AdditionalSections["SomeValue"]["Data"].Should().Be("123");
    }

    [Fact]
    public void ReadString_SectionEndingWithDenotation_NotHexPrefixed_PreservedInAdditionalSections()
    {
        // Arrange - "CustomDenotation" ends with "Denotation" but prefix is not hex
        var content = BuildMinimalDcf(extraSections: @"
[CustomDenotation]
Label=Test
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().ContainKey("CustomDenotation");
    }

    [Fact]
    public void ReadString_SectionWithMetadata_NotModule_PreservedInAdditionalSections()
    {
        // Arrange - "Metadata" starts with "M" but has no digit after "M"
        var content = BuildMinimalDcf(extraSections: @"
[Metadata]
Author=Test
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().ContainKey("Metadata");
    }

    #endregion

    #region DynamicChannels Tests

    [Fact]
    public void ReadString_DynamicChannels_ParsesSegments()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[DynamicChannels]
NrOfSeg=2
Type1=0x0007
Dir1=ro
Range1=0xA080-0xA0BF
PPOffset1=0
Type2=0x0005
Dir2=rww
Range2=0xA0C0-0xA0FF
PPOffset2=64
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DynamicChannels.Should().NotBeNull();
        result.DynamicChannels!.Segments.Should().HaveCount(2);
        result.DynamicChannels.Segments[0].Type.Should().Be(0x0007);
        result.DynamicChannels.Segments[0].Dir.Should().Be(AccessType.ReadOnly);
        result.DynamicChannels.Segments[0].Range.Should().Be("0xA080-0xA0BF");
        result.DynamicChannels.Segments[0].PPOffset.Should().Be(0);
        result.DynamicChannels.Segments[1].Type.Should().Be(0x0005);
        result.DynamicChannels.Segments[1].Dir.Should().Be(AccessType.ReadWriteOutput);
        result.DynamicChannels.Segments[1].Range.Should().Be("0xA0C0-0xA0FF");
        result.DynamicChannels.Segments[1].PPOffset.Should().Be(64);
    }

    [Fact]
    public void ReadString_NoDynamicChannels_ReturnsNull()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.DynamicChannels.Should().BeNull();
    }

    [Fact]
    public void ReadString_DynamicChannelsNotInAdditionalSections()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[DynamicChannels]
NrOfSeg=1
Type1=0x0007
Dir1=ro
Range1=0xA080-0xA0BF
PPOffset1=0
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().NotContainKey("DynamicChannels");
    }

    #endregion

    #region Tools Tests

    [Fact]
    public void ReadString_ToolsSections_ParsesTools()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[Tools]
Items=2
[Tool1]
Name=EDS Checker
Command=checker.exe $EDS
[Tool2]
Name=Configurator
Command=config.exe $DCF $NODEID
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Tools.Should().HaveCount(2);
        result.Tools[0].Name.Should().Be("EDS Checker");
        result.Tools[0].Command.Should().Be("checker.exe $EDS");
        result.Tools[1].Name.Should().Be("Configurator");
        result.Tools[1].Command.Should().Be("config.exe $DCF $NODEID");
    }

    [Fact]
    public void ReadString_NoToolsSection_ReturnsEmptyList()
    {
        // Arrange
        var content = BuildMinimalDcf();

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Tools.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_ToolsSectionsNotInAdditionalSections()
    {
        // Arrange
        var content = BuildMinimalDcf(extraSections: @"
[Tools]
Items=1
[Tool1]
Name=Checker
Command=check.exe
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().NotContainKey("Tools");
        result.AdditionalSections.Should().NotContainKey("Tool1");
    }

    [Fact]
    public void ReadString_SectionNamedTool_PreservedInAdditionalSections()
    {
        // Arrange — section named exactly "Tool" (length 4, no numeric suffix)
        var content = BuildMinimalDcf(extraSections: @"
[Tool]
SomeKey=SomeValue
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.AdditionalSections.Should().ContainKey("Tool");
    }

    [Fact]
    public void ReadString_ToolZeroSection_PreservedInAdditionalSections()
    {
        // Arrange — Tool0 has toolNumber < 1
        var content = BuildMinimalDcf(extraSections: @"
[Tools]
Items=1
[Tool1]
Name=Checker
Command=check.exe
[Tool0]
Name=Zero
Command=zero.exe
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Tools.Should().HaveCount(1);
        result.AdditionalSections.Should().ContainKey("Tool0");
    }

    [Fact]
    public void ReadString_OrphanToolSection_PreservedInAdditionalSections()
    {
        // Arrange — Tool1 without [Tools] section
        var content = BuildMinimalDcf(extraSections: @"
[Tool1]
Name=Orphan
Command=orphan.exe
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Tools.Should().BeEmpty();
        result.AdditionalSections.Should().ContainKey("Tool1");
    }

    [Fact]
    public void ReadString_ToolSectionBeyondItems_PreservedInAdditionalSections()
    {
        // Arrange — Items=1 but Tool2 also present
        var content = BuildMinimalDcf(extraSections: @"
[Tools]
Items=1
[Tool1]
Name=Checker
Command=check.exe
[Tool2]
Name=Extra
Command=extra.exe
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Tools.Should().HaveCount(1);
        result.AdditionalSections.Should().NotContainKey("Tool1");
        result.AdditionalSections.Should().ContainKey("Tool2");
    }

    [Fact]
    public void ReadString_ToolSectionWithNonNumericSuffix_PreservedInAdditionalSections()
    {
        // Arrange — ToolABC is not a valid ToolX section
        var content = BuildMinimalDcf(extraSections: @"
[Tools]
Items=1
[Tool1]
Name=Checker
Command=check.exe
[ToolABC]
Name=Invalid
Command=invalid.exe
");

        // Act
        var result = _reader.ReadString(content);

        // Assert
        result.Tools.Should().HaveCount(1);
        result.AdditionalSections.Should().ContainKey("ToolABC");
    }

    [Fact]
    public void ReadString_SectionNamedValue_WithNoHexPrefix_PreservedInAdditionalSections()
    {
        // Arrange - a section named exactly "Value" is not treated as a hex-prefixed
        // compact storage section and should be preserved in AdditionalSections.
        var content = BuildMinimalDcf(extraSections: @"
[Value]
SomeKey=SomeData
");

        // Act
        var result = _reader.ReadString(content);

        // Assert - "Value" is not a known hex-prefixed section, so it lands in AdditionalSections
        result.AdditionalSections.Should().ContainKey("Value");
    }

    #endregion
}
