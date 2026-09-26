namespace EdsDcfNet.Tests.Checker;

using System.Globalization;
using EdsDcfNet;
using EdsDcfNet.Checker;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// DCF <c>[xxxxValue]</c> keys above <c>CompactSubObj</c> are applied only when an
/// explicit sub-object section exists. Otherwise <c>ApplyCompactListSection</c> drops
/// the commissioned value and default library validation stays silent, so the checker
/// must fail the file (VAL008).
/// </summary>
public class RawObjectCheckerCompactValueTests
{
    private const string DeviceInfo = @"
[DeviceInfo]
VendorName=Test
";
    [Fact]
    public void Check_OrphanCompactValueAboveCompactSubObj_ReportsVal008_AndReaderDropsIt()
    {
        // Arrange — CompactSubObj=2 synthesizes sub-indexes 0..2. Key 3 has no [2000sub3].
        const string content = DeviceInfo + @"
[DeviceComissioning]
NodeID=1

[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Config
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0
CompactSubObj=2

[2000Value]
1=4
3=7
";

        // Act
        var findings = Check(content);
        var loaded = CanOpenFile.Dcf.ReadString(content);

        // Assert
        var orphan = findings.Should().ContainSingle(f => f.Code == "VAL008").Subject;
        orphan.Severity.Should().Be(Severity.Error);
        orphan.Section.Should().Be("2000Value");
        orphan.Key.Should().Be("3");
        orphan.Value.Should().Be("7");
        orphan.Message.Should().Contain("[2000sub3]");
        orphan.Message.Should().Contain("1..2");
        findings.Should().NotContain(f => f.Code == "VAL008" && f.Key == "1");

        var obj = loaded.ObjectDictionary.Objects[0x2000];
        obj.SubObjects.Should().NotContainKey(3);
        obj.SubObjects[1].ParameterValue.Should().Be("4");
    }

    [Theory]
    [InlineData(2, "3")]
    [InlineData(1, "2")]
    [InlineData(253, "254")]
    public void Check_CompactValueAboveCompactSubObj_ReportsVal008(int compactSubObj, string key)
    {
        // Arrange
        var content = CompactDcf(compactSubObj, key + "=7");

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f =>
            f.Code == "VAL008" &&
            f.Severity == Severity.Error &&
            f.Section == "2000Value" &&
            f.Key == key &&
            f.Value == "7");
    }

    [Theory]
    [InlineData(2, "1")]
    [InlineData(2, "2")]
    [InlineData(254, "254")]
    [InlineData(255, "254")]
    public void Check_CompactValueWithinCompactSubObj_DoesNotReportVal008(int compactSubObj, string key)
    {
        // Arrange — 254 is the highest listable sub-index; CompactSubObj=255 still covers it.
        var content = CompactDcf(compactSubObj, key + "=7");

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().NotContain(f => f.Code == "VAL008");
    }

    [Fact]
    public void Check_CompactValue_AtMaxListableSubIndex_InvalidValue_ReportsVal001NotVal008()
    {
        // Arrange — sub-index 254 is synthesized from CompactSubObj=254, so 999 is a range error.
        var content = CompactDcf(254, "254=999");

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f => f.Code == "VAL001" && f.Section == "2000Value" && f.Key == "254");
        findings.Should().NotContain(f => f.Code == "VAL008");
    }

    [Fact]
    public void Check_ExplicitSubAboveCompactSubObj_DoesNotReportVal008()
    {
        // Arrange — [2000sub3] exists, so the reader applies 3=7 to that sub-object.
        const string content = DeviceInfo + @"
[DeviceComissioning]
NodeID=1

[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Config
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0
CompactSubObj=2

[2000sub3]
ParameterName=Extra
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0

[2000Value]
3=7
";

        // Act
        var findings = Check(content);
        var loaded = CanOpenFile.Dcf.ReadString(content);

        // Assert
        findings.Should().NotContain(f => f.Code == "VAL008");
        loaded.ObjectDictionary.Objects[0x2000].SubObjects[3].ParameterValue.Should().Be("7");
    }

    [Fact]
    public void Check_ExplicitSubAboveCompactSubObj_OutOfRangeValue_ReportsVal001NotVal008()
    {
        // Arrange
        const string content = @"
[DeviceComissioning]
NodeID=1

[2000]
ParameterName=Config
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0
CompactSubObj=2

[2000sub3]
ParameterName=Extra
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0

[2000Value]
3=999
";

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f => f.Code == "VAL001" && f.Section == "2000Value" && f.Key == "3");
        findings.Should().NotContain(f => f.Code == "VAL008");
    }

    [Fact]
    public void Check_ExplicitSubMissingDataType_AboveCompactSubObj_DoesNotReportVal008()
    {
        // Arrange — the section is still loaded, so the value is applied. Missing DataType
        // is OBJ004, not a discarded compact value.
        const string content = DeviceInfo + @"
[DeviceComissioning]
NodeID=1

[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Config
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0
CompactSubObj=2

[2000sub3]
ParameterName=Extra
ObjectType=0x7
AccessType=rw
PDOMapping=0

[2000Value]
3=7
";

        // Act
        var findings = Check(content);
        var loaded = CanOpenFile.Dcf.ReadString(content);

        // Assert
        findings.Should().Contain(f => f.Code == "OBJ004" && f.Section == "2000sub3");
        findings.Should().NotContain(f => f.Code == "VAL008");
        loaded.ObjectDictionary.Objects[0x2000].SubObjects[3].ParameterValue.Should().Be("7");
    }

    [Theory]
    [InlineData("3=")]
    [InlineData("3=   ")]
    [InlineData("0=7")]
    [InlineData("255=7")]
    [InlineData("NrOfEntries=1")]
    [InlineData("Comment=7")]
    public void Check_IgnoredCompactValueKey_DoesNotReportVal008(string entry)
    {
        // Arrange — empty values, sub-index 0, 0xFF, and non-numeric keys are not compact values.
        var content = CompactDcf(2, entry);

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().NotContain(f => f.Code == "VAL008");
    }

    [Fact]
    public void Check_MultipleOrphanCompactValues_ReportsEach()
    {
        // Arrange
        var content = CompactDcf(2, "3=7\n10=8");

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f => f.Code == "VAL008" && f.Key == "3" && f.Value == "7");
        findings.Should().Contain(f =>
            f.Code == "VAL008" &&
            f.Key == "10" &&
            f.Value == "8" &&
            f.Message.Contains("[2000subA]", StringComparison.Ordinal));
    }

    [Fact]
    public void Check_InRangeInvalidValue_AndOrphan_ReportsBoth()
    {
        // Arrange
        var content = CompactDcf(2, "1=999\n3=7");

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f => f.Code == "VAL001" && f.Key == "1");
        findings.Should().Contain(f => f.Code == "VAL008" && f.Severity == Severity.Error && f.Key == "3");
    }

    [Fact]
    public void Check_EdsValueSectionAboveCompactSubObj_DoesNotReportVal008()
    {
        // Arrange — [xxxxValue] is DCF storage. An EDS does not commission those entries.
        const string content = @"
[2000]
ParameterName=Config
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0
CompactSubObj=2

[2000Value]
3=7
";

        // Act
        var findings = Check(content, isDcf: false);

        // Assert
        findings.Should().NotContain(f => f.Code == "VAL008");
    }

    private static string CompactDcf(int compactSubObj, string valueEntries) =>
        DeviceInfo + @"
[DeviceComissioning]
NodeID=1

[2000]
ParameterName=Config
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0
CompactSubObj=" + compactSubObj.ToString(CultureInfo.InvariantCulture) + @"

[2000Value]
" + valueEntries + "\n";

    private static List<Finding> Check(string content, bool isDcf = true)
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
