namespace EdsDcfNet.Tests.Checker;

using EdsDcfNet;
using EdsDcfNet.Checker;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// Object and sub-index section names the checker accepts must follow
/// <c>CanOpenReaderBase</c>: unpadded hex (<c>[40]</c>, <c>[40sub0]</c>, <c>[1018sub1]</c>).
/// Four-digit index spellings stay valid. Zero-padded sub names are an error because
/// <c>ParseSubObject</c> never probes them.
/// </summary>
public class RawObjectCheckerSectionNameTests
{
    [Theory]
    [InlineData("40")]
    [InlineData("0040")]
    [InlineData("20")]
    public void Check_IndexSpelling_ChecksTheSection(string sectionName)
    {
        // Arrange — UNSIGNED8 DefaultValue 999 is VAL001 only when the section is collected.
        var content = @"
[OptionalObjects]
SupportedObjects=1
1=0x" + sectionName + @"

[" + sectionName + @"]
ParameterName=Custom
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=999
PDOMapping=0
";

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f => f.Code == "VAL001" && f.Section == sectionName);
        findings.Should().NotContain(f => f.Code == "LST002");
    }

    [Fact]
    public void Check_ShortSubIndex_IsCollected()
    {
        // Arrange — VAR must not have sub-indices; OBJ006 fires only when [40sub0] is seen.
        const string content = @"
[40]
ParameterName=Custom
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[40sub0]
ParameterName=Hidden
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0
";

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f => f.Code == "OBJ006" && f.Section == "40");
        findings.Should().NotContain(f => f.Code == "OBJ011");
    }

    [Fact]
    public void Check_DuplicateIndexSpellings_ReportsIni002()
    {
        // Arrange
        const string content = @"
[40]
ParameterName=First
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[0040]
ParameterName=Second
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0
";

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f =>
            f.Code == "INI002" &&
            f.Section == "0040" &&
            f.Message.Contains("[40]", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("1018sub01", "[1018sub1]")]
    [InlineData("1018sub0A", "[1018subA]")]
    [InlineData("1018sub00", "[1018sub0]")]
    [InlineData("40sub01", "[40sub1]")]
    public void Check_PaddedSubIndex_ReportsObj011(string sectionName, string readerName)
    {
        // Arrange
        var content = "[" + sectionName + @"]
ParameterName=Padded
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0
PDOMapping=0
";

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f =>
            f.Code == "OBJ011" &&
            f.Severity == Severity.Error &&
            f.Section == sectionName &&
            f.Message.Contains(readerName, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("1018sub1")]
    [InlineData("1018subA")]
    [InlineData("1018suba")]
    [InlineData("1018sub0")]
    [InlineData("1018sub10")]
    [InlineData("40sub0")]
    public void Check_UnpaddedSubIndex_DoesNotReportObj011(string sectionName)
    {
        // Arrange
        var content = "[" + sectionName + @"]
ParameterName=Unpadded
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0
PDOMapping=0
";

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().NotContain(f => f.Code == "OBJ011");
    }

    [Fact]
    public void Check_PaddedAndUnpaddedSub_ReportsObj011AndIni002()
    {
        // Arrange — reader keeps [1018sub1]; the padded spelling is the same sub-index.
        const string content = @"
[1018]
ParameterName=Identity
ObjectType=0x9
SubNumber=1

[1018sub1]
ParameterName=Vendor
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=1
PDOMapping=0

[1018sub01]
ParameterName=Vendor again
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=1
PDOMapping=0
";

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f => f.Code == "OBJ011" && f.Section == "1018sub01");
        findings.Should().Contain(f => f.Code == "INI002" && f.Section == "1018sub01");
    }

    [Fact]
    public void Check_ShortIndex_DcfValueSection_IsChecked()
    {
        // Arrange — library writes [40Value], not [0040Value]. 999 does not fit UNSIGNED8.
        const string content = @"
[DeviceComissioning]
NodeID=1

[40]
ParameterName=Compact
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0
CompactSubObj=1

[40Value]
1=999
";

        // Act
        var findings = Check(content, isDcf: true);

        // Assert
        findings.Should().Contain(f => f.Code == "VAL001" && f.Section == "40Value");
    }

    [Fact]
    public void ReadString_PaddedSubIndex_ReaderDropsIt_UnpaddedIsLoaded()
    {
        // Arrange
        const string padded = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x1018

[1018]
ParameterName=Identity
ObjectType=0x9
SubNumber=1

[1018sub01]
ParameterName=Vendor
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=1
PDOMapping=0
";
        const string unpadded = @"
[DeviceInfo]
VendorName=Test

[MandatoryObjects]
SupportedObjects=1
1=0x1018

[1018]
ParameterName=Identity
ObjectType=0x9
SubNumber=1

[1018sub1]
ParameterName=Vendor
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=1
PDOMapping=0
";

        // Act
        var dropped = CanOpenFile.Eds.ReadString(padded);
        var loaded = CanOpenFile.Eds.ReadString(unpadded);

        // Assert — checker OBJ011 matches this probe, not a separate naming policy.
        dropped.ObjectDictionary.Objects[0x1018].SubObjects.Should().BeEmpty();
        loaded.ObjectDictionary.Objects[0x1018].SubObjects.Should().ContainKey(1);
        loaded.ObjectDictionary.Objects[0x1018].SubObjects[1].ParameterName.Should().Be("Vendor");
    }

    private static List<Finding> Check(string content, bool isDcf = false)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "edsdcf-check-" + Guid.NewGuid().ToString("N") + (isDcf ? ".dcf" : ".eds"));
        File.WriteAllText(path, content);
        try
        {
            var findings = new List<Finding>();
            var document = RawIniDocument.Parse(path, findings);
            new RawObjectChecker(path, document, isDcf, findings).Run();
            return findings;
        }
        finally
        {
            File.Delete(path);
        }
    }
}
