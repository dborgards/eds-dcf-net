namespace EdsDcfNet.Tests.Parsers;

public partial class EdsReaderTests
{
    private static string ToolsContent(string sections) => @"
[FileInfo]
FileName=test.eds
[DeviceInfo]
VendorName=Test
VendorNumber=1
ProductName=Test
ProductNumber=1
RevisionNumber=1
OrderCode=Test
BaudRate_10=0
BaudRate_20=0
BaudRate_50=0
BaudRate_125=1
BaudRate_250=1
BaudRate_500=1
BaudRate_800=0
BaudRate_1000=0
SimpleBootUpMaster=0
SimpleBootUpSlave=1
Granularity=8
DynamicChannelsSupported=0
GroupMessaging=0
NrOfRXPDO=0
NrOfTXPDO=0
LSS_Supported=0
[MandatoryObjects]
SupportedObjects=2
1=0x1000
2=0x1001
[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
[1001]
ParameterName=Error Register
ObjectType=0x7
DataType=0x0005
AccessType=ro
" + sections;

    #region Tools Tests

    [Fact]
    public void ReadString_ToolsSections_ParsesTools()
    {
        var content = ToolsContent(@"[Tools]
Items=2
[Tool1]
Name=EDS Checker
Command=checker.exe $EDS
[Tool2]
Name=Configurator
Command=config.exe $DCF $NODEID
");

        var result = _reader.ReadString(content);

        result.Tools.Should().HaveCount(2);
        result.Tools[0].Name.Should().Be("EDS Checker");
        result.Tools[0].Command.Should().Be("checker.exe $EDS");
        result.Tools[1].Name.Should().Be("Configurator");
        result.Tools[1].Command.Should().Be("config.exe $DCF $NODEID");
    }

    [Fact]
    public void ReadString_NoToolsSection_ReturnsEmptyList()
    {
        var result = _reader.ReadString(ToolsContent(string.Empty));

        result.Tools.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_Tools_MissingToolSection_SkipsEntry()
    {
        var content = ToolsContent(@"[Tools]
Items=2
[Tool1]
Name=Checker
Command=check.exe
");

        var result = _reader.ReadString(content);

        result.Tools.Should().ContainSingle();
        result.Tools[0].Name.Should().Be("Checker");
    }

    [Fact]
    public void ReadString_ToolsSections_NotInAdditionalSections()
    {
        var content = ToolsContent(@"[Tools]
Items=1
[Tool1]
Name=Checker
Command=check.exe
");

        var result = _reader.ReadString(content);

        result.AdditionalSections.Should().NotContainKey("Tools");
        result.AdditionalSections.Should().NotContainKey("Tool1");
    }

    [Fact]
    public void ReadString_ToolSectionWithNonNumericSuffix_PreservedInAdditionalSections()
    {
        var content = ToolsContent(@"[Tools]
Items=1
[Tool1]
Name=Checker
Command=check.exe
[ToolABC]
Name=Invalid
Command=invalid.exe
");

        var result = _reader.ReadString(content);

        result.Tools.Should().ContainSingle();
        result.AdditionalSections.Should().ContainKey("ToolABC");
    }

    [Fact]
    public void ReadString_SectionNamedTool_PreservedInAdditionalSections()
    {
        var content = ToolsContent(@"[Tool]
SomeKey=SomeValue
");

        var result = _reader.ReadString(content);

        result.AdditionalSections.Should().ContainKey("Tool");
    }

    [Fact]
    public void ReadString_ToolZeroSection_PreservedInAdditionalSections()
    {
        var content = ToolsContent(@"[Tools]
Items=1
[Tool1]
Name=Checker
Command=check.exe
[Tool0]
Name=Zero
Command=zero.exe
");

        var result = _reader.ReadString(content);

        result.Tools.Should().ContainSingle();
        result.AdditionalSections.Should().ContainKey("Tool0");
    }

    [Fact]
    public void ReadString_OrphanToolSection_PreservedInAdditionalSections()
    {
        var content = ToolsContent(@"[Tool1]
Name=Orphan
Command=orphan.exe
");

        var result = _reader.ReadString(content);

        result.Tools.Should().BeEmpty();
        result.AdditionalSections.Should().ContainKey("Tool1");
    }

    [Fact]
    public void ReadString_ToolSectionBeyondItems_PreservedInAdditionalSections()
    {
        var content = ToolsContent(@"[Tools]
Items=1
[Tool1]
Name=Checker
Command=check.exe
[Tool2]
Name=Extra
Command=extra.exe
");

        var result = _reader.ReadString(content);

        result.Tools.Should().ContainSingle();
        result.AdditionalSections.Should().NotContainKey("Tool1");
        result.AdditionalSections.Should().ContainKey("Tool2");
    }

    #endregion
}
