namespace EdsDcfNet.Tests.Checker;

using System.Globalization;
using EdsDcfNet;
using EdsDcfNet.Checker;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// DCF <c>[xxxxValue]</c> keys are applied only when the reader has a sub-object for
/// that index: a synthesized compact entry, or an unpadded <c>[XXXXsubN]</c> section.
/// A key above <c>CompactSubObj</c> whose only section is a zero-padded alias, and a
/// key on an expanded object (<c>CompactSubObj</c> absent or zero) with no matching
/// sub-object, are both dropped by <c>ApplyCompactListSection</c>. Default library
/// validation stays silent, so the checker must fail the file (VAL008).
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

    [Theory]
    [InlineData("2000sub03")]
    [InlineData("2000sub003")]
    [InlineData("02000sub3")]
    public void Check_PaddedOnlySubAboveCompactSubObj_ReportsVal008_AndReaderDropsIt(string paddedSection)
    {
        // Arrange — CompactSubObj=2 synthesizes 0..2. [2000sub3] is the only name
        // ParseSubObject loads; a zero-padded alias is not that section, so 3=7 is discarded.
        var content = DeviceInfo + @"
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

[" + paddedSection + @"]
ParameterName=Alias
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=1
LowLimit=0
HighLimit=1
PDOMapping=0

[2000Value]
1=4
3=7
";

        // Act
        var findings = Check(content);
        var loaded = CanOpenFile.Dcf.ReadString(content);

        // Assert — 7 would fail the alias HighLimit, but the reader never loads that section.
        var orphan = findings.Should().ContainSingle(f => f.Code == "VAL008").Subject;
        orphan.Severity.Should().Be(Severity.Error);
        orphan.Section.Should().Be("2000Value");
        orphan.Key.Should().Be("3");
        orphan.Value.Should().Be("7");
        orphan.Message.Should().Contain("[2000sub3]");
        orphan.Message.Should().Contain("1..2");
        findings.Should().Contain(f => f.Code == "OBJ011" && f.Section == paddedSection);
        findings.Should().NotContain(f => f.Code == "VAL004" && f.Section == "2000Value");
        findings.Should().NotContain(f => f.Code == "VAL008" && f.Key == "1");

        var obj = loaded.ObjectDictionary.Objects[0x2000];
        obj.SubObjects.Should().NotContainKey(3);
        obj.SubObjects[1].ParameterValue.Should().Be("4");
    }

    [Fact]
    public void Check_PaddedAndCanonicalSubAboveCompactSubObj_DoesNotReportVal008()
    {
        // Arrange — the unpadded section is what the reader loads, so 3=7 is applied.
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

[2000sub03]
ParameterName=Alias
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0

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
        findings.Should().Contain(f => f.Code == "OBJ011" && f.Section == "2000sub03");
        findings.Should().NotContain(f => f.Code == "VAL008");
        loaded.ObjectDictionary.Objects[0x2000].SubObjects[3].ParameterValue.Should().Be("7");
    }

    [Fact]
    public void Check_PaddedOnlySubWithinCompactRange_DoesNotReportVal008()
    {
        // Arrange — sub-index 1 is synthesized from CompactSubObj even when the only
        // [2000sub1] spelling is padded. The commissioned value is applied to that template.
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

[2000sub01]
ParameterName=Alias
ObjectType=0x7
DataType=0x0007
AccessType=rw
DefaultValue=1
PDOMapping=0

[2000Value]
1=4
";

        // Act
        var findings = Check(content);
        var loaded = CanOpenFile.Dcf.ReadString(content);

        // Assert — 4 fits the parent UNSIGNED8 template. The alias type is not applied.
        findings.Should().Contain(f => f.Code == "OBJ011" && f.Section == "2000sub01");
        findings.Should().NotContain(f => f.Code == "VAL008");
        loaded.ObjectDictionary.Objects[0x2000].SubObjects[1].ParameterValue.Should().Be("4");
        loaded.ObjectDictionary.Objects[0x2000].SubObjects[1].DataType.Should().Be(0x0005);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Check_OrphanValueOnExpandedObject_ReportsVal008_AndReaderDropsIt(bool compactSubObjZero)
    {
        // Arrange — sub-indexes 0 and 1 only. Key 2 has no reader-visible [2000sub2].
        // compactSubObjZero writes CompactSubObj=0; otherwise the key is absent.
        var content = ExpandedDcf(compactSubObjZero, "1=4\n2=7");

        // Act
        var findings = Check(content);
        var loaded = CanOpenFile.Dcf.ReadString(content);

        // Assert
        var orphan = findings.Should().ContainSingle(f => f.Code == "VAL008").Subject;
        orphan.Severity.Should().Be(Severity.Error);
        orphan.Section.Should().Be("2000Value");
        orphan.Key.Should().Be("2");
        orphan.Value.Should().Be("7");
        orphan.Message.Should().Contain("[2000sub2]");
        findings.Should().NotContain(f => f.Code == "VAL008" && f.Key == "1");

        var obj = loaded.ObjectDictionary.Objects[0x2000];
        obj.SubObjects.Should().NotContainKey(2);
        obj.SubObjects[1].ParameterValue.Should().Be("4");
    }

    [Theory]
    [InlineData(false, "1")]
    [InlineData(true, "1")]
    [InlineData(false, "254")]
    [InlineData(true, "254")]
    public void Check_ExpandedValueWithoutSubObject_ReportsVal008(bool compactSubObjZero, string key)
    {
        // Arrange — 1 and 254 are the compact-list bounds. Neither sub-index is defined.
        var content = ExpandedDcf(compactSubObjZero, key + "=7", includeSub1: false);

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

    [Fact]
    public void Check_ExpandedValue_AtMaxListableSubIndex_WithSection_DoesNotReportVal008()
    {
        // Arrange — sub-index 254 is explicit, so 7 is applied rather than discarded.
        const string content = DeviceInfo + @"
[DeviceComissioning]
NodeID=1

[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Record
ObjectType=0x9
SubNumber=2

[2000sub0]
ParameterName=Highest
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=254
PDOMapping=0

[2000subFE]
ParameterName=Last
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0

[2000Value]
254=7
";

        // Act
        var findings = Check(content);
        var loaded = CanOpenFile.Dcf.ReadString(content);

        // Assert
        findings.Should().NotContain(f => f.Code == "VAL008");
        loaded.ObjectDictionary.Objects[0x2000].SubObjects[254].ParameterValue.Should().Be("7");
    }

    [Fact]
    public void Check_ExpandedExplicitSub_DoesNotReportVal008()
    {
        // Arrange
        var content = ExpandedDcf(compactSubObjZero: false, "1=4");

        // Act
        var findings = Check(content);
        var loaded = CanOpenFile.Dcf.ReadString(content);

        // Assert
        findings.Should().NotContain(f => f.Code == "VAL008");
        loaded.ObjectDictionary.Objects[0x2000].SubObjects[1].ParameterValue.Should().Be("4");
    }

    [Fact]
    public void Check_ExpandedExplicitSubMissingDataType_DoesNotReportVal008()
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
ParameterName=Record
ObjectType=0x9
SubNumber=2

[2000sub0]
ParameterName=Highest
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[2000sub1]
ParameterName=Entry
ObjectType=0x7
AccessType=rw
PDOMapping=0

[2000Value]
1=7
";

        // Act
        var findings = Check(content);
        var loaded = CanOpenFile.Dcf.ReadString(content);

        // Assert
        findings.Should().Contain(f => f.Code == "OBJ004" && f.Section == "2000sub1");
        findings.Should().NotContain(f => f.Code == "VAL008");
        loaded.ObjectDictionary.Objects[0x2000].SubObjects[1].ParameterValue.Should().Be("7");
    }

    [Fact]
    public void Check_ExpandedExplicitSub_OutOfRangeValue_ReportsVal001NotVal008()
    {
        // Arrange
        var content = ExpandedDcf(compactSubObjZero: false, "1=999");

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f => f.Code == "VAL001" && f.Section == "2000Value" && f.Key == "1");
        findings.Should().NotContain(f => f.Code == "VAL008");
    }

    [Theory]
    [InlineData("2000sub02")]
    [InlineData("02000sub2")]
    public void Check_ExpandedPaddedOnlySub_ReportsVal008_AndReaderDropsIt(string paddedSection)
    {
        // Arrange — [2000sub2] is missing. The padded alias is not loaded, so 2=7 is discarded.
        var content = DeviceInfo + @"
[DeviceComissioning]
NodeID=1

[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Record
ObjectType=0x9
SubNumber=2

[2000sub0]
ParameterName=Highest
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[2000sub1]
ParameterName=Entry
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0

[" + paddedSection + @"]
ParameterName=Alias
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0

[2000Value]
2=7
";

        // Act
        var findings = Check(content);
        var loaded = CanOpenFile.Dcf.ReadString(content);

        // Assert
        var orphan = findings.Should().ContainSingle(f => f.Code == "VAL008").Subject;
        orphan.Key.Should().Be("2");
        orphan.Value.Should().Be("7");
        orphan.Message.Should().Contain("[2000sub2]");
        findings.Should().Contain(f => f.Code == "OBJ011" && f.Section == paddedSection);
        loaded.ObjectDictionary.Objects[0x2000].SubObjects.Should().NotContainKey(2);
    }

    [Fact]
    public void Check_MultipleOrphanExpandedValues_ReportsEach()
    {
        // Arrange
        var content = ExpandedDcf(compactSubObjZero: false, "2=7\n10=8", includeSub1: true);

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f => f.Code == "VAL008" && f.Key == "2" && f.Value == "7");
        findings.Should().Contain(f =>
            f.Code == "VAL008" &&
            f.Key == "10" &&
            f.Value == "8" &&
            f.Message.Contains("[2000subA]", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("2=")]
    [InlineData("2=   ")]
    [InlineData("0=7")]
    [InlineData("255=7")]
    [InlineData("NrOfEntries=1")]
    [InlineData("Comment=7")]
    public void Check_IgnoredExpandedValueKey_DoesNotReportVal008(string entry)
    {
        // Arrange — empty values, sub-index 0, 0xFF, and non-numeric keys are not compact values.
        var content = ExpandedDcf(compactSubObjZero: true, entry);

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().NotContain(f => f.Code == "VAL008");
    }

    [Fact]
    public void Check_EdsExpandedValue_DoesNotReportVal008()
    {
        // Arrange — [xxxxValue] is DCF storage. An EDS does not commission those entries.
        const string content = @"
[2000]
ParameterName=Record
ObjectType=0x9
SubNumber=2

[2000sub0]
ParameterName=Highest
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[2000sub1]
ParameterName=Entry
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0

[2000Value]
2=7
";

        // Act
        var findings = Check(content, isDcf: false);

        // Assert
        findings.Should().NotContain(f => f.Code == "VAL008");
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

    private static string ExpandedDcf(bool compactSubObjZero, string valueEntries, bool includeSub1 = true)
    {
        var subNumber = includeSub1 ? 2 : 1;
        var compactLine = compactSubObjZero ? "CompactSubObj=0\n" : string.Empty;
        var sub1 = includeSub1
            ? @"
[2000sub1]
ParameterName=Entry
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0
"
            : string.Empty;

        return DeviceInfo + @"
[DeviceComissioning]
NodeID=1

[ManufacturerObjects]
SupportedObjects=1
1=0x2000

[2000]
ParameterName=Record
ObjectType=0x9
SubNumber=" + subNumber.ToString(CultureInfo.InvariantCulture) + @"
" + compactLine + @"
[2000sub0]
ParameterName=Highest
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=" + (includeSub1 ? "1" : "0") + @"
PDOMapping=0
" + sub1 + @"
[2000Value]
" + valueEntries + "\n";
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
