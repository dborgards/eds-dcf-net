namespace EdsDcfNet.Tests.Checker;

using EdsDcfNet;
using EdsDcfNet.Checker;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// Object, sub-index, and compact-value section names the checker accepts must follow
/// <c>CanOpenReaderBase</c>: unpadded hex (<c>[40]</c>, <c>[40sub0]</c>, <c>[40Value]</c>).
/// A leading zero is OBJ011 because <c>ParseObject</c>, <c>ParseSubObject</c>, and
/// <c>ApplyCompactListSection</c> never probe the padded spelling. Over-width leading
/// zeros that still fit the index (<c>[00020]</c>, <c>[00020sub1]</c>, <c>[20sub001]</c>)
/// are the same error.
/// </summary>
public class RawObjectCheckerSectionNameTests
{
    [Theory]
    [InlineData("40")]
    [InlineData("20")]
    [InlineData("1000")]
    [InlineData("A")]
    [InlineData("a")]
    [InlineData("0")]
    public void Check_UnpaddedIndex_ChecksTheSection_WithoutObj011(string sectionName)
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
        findings.Should().NotContain(f => f.Code == "OBJ011");
    }

    [Theory]
    [InlineData("0040", "[40]")]
    [InlineData("020", "[20]")]
    [InlineData("0020", "[20]")]
    [InlineData("00020", "[20]")]
    [InlineData("0100", "[100]")]
    [InlineData("0A", "[A]")]
    [InlineData("00", "[0]")]
    [InlineData("00000", "[0]")]
    [InlineData("0FFF", "[FFF]")]
    [InlineData("0FFFF", "[FFFF]")]
    public void Check_PaddedIndex_ReportsObj011_AndStillChecksTheSection(string sectionName, string readerName)
    {
        // Arrange — the section is still validated, but the padded name is an error
        // even when every value would otherwise be accepted.
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
        findings.Should().Contain(f =>
            f.Code == "OBJ011" &&
            f.Severity == Severity.Error &&
            f.Section == sectionName &&
            f.Message.Contains(readerName, StringComparison.Ordinal));
    }

    [Fact]
    public void Check_PaddedIndexWithValidValues_ReportsObj011()
    {
        // Arrange — in-range values used to let edsdcf-check exit 0 while the reader
        // dropped [0020] (it only probes [20]).
        const string content = @"
[OptionalObjects]
SupportedObjects=1
1=0x20

[0020]
ParameterName=Custom
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
            f.Code == "OBJ011" &&
            f.Severity == Severity.Error &&
            f.Section == "0020" &&
            f.Message.Contains("[20]", StringComparison.Ordinal));
        findings.Should().NotContain(f => f.Code == "VAL001");
        findings.Should().NotContain(f => f.Code == "LST002");
    }

    [Fact]
    public void Check_MissingLowIndex_Lst002NamesUnpaddedSection()
    {
        // Arrange
        const string content = @"
[OptionalObjects]
SupportedObjects=1
1=0x20
";

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f =>
            f.Code == "LST002" &&
            f.Message.Contains("no [20] section", StringComparison.Ordinal));
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
        findings.Should().Contain(f => f.Code == "OBJ011" && f.Section == "0040");
    }

    [Theory]
    [InlineData("0040", "40")]
    [InlineData("00040", "40")]
    public void Check_PaddedIndexBeforeCanonical_ValidatesTheSectionTheReaderLoads(
        string paddedName,
        string canonicalName)
    {
        // Arrange — 999 does not fit UNSIGNED8. The reader loads [40], so that
        // section must be checked even when the padded alias appears first.
        var content = @"
[OptionalObjects]
SupportedObjects=1
1=0x40

[" + paddedName + @"]
ParameterName=Padded
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[" + canonicalName + @"]
ParameterName=Canonical
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=999
PDOMapping=0
";

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f => f.Code == "OBJ011" && f.Section == paddedName);
        findings.Should().Contain(f =>
            f.Code == "INI002" &&
            f.Section == canonicalName &&
            f.Message.Contains("[" + paddedName + "]", StringComparison.Ordinal));
        findings.Should().Contain(f => f.Code == "VAL001" && f.Section == canonicalName);
        findings.Should().NotContain(f => f.Code == "VAL001" && f.Section == paddedName);
        findings.Should().NotContain(f => f.Code == "LST002");
    }

    [Fact]
    public void Check_UnlistedOverWidthPaddedIndex_ReportsObj011()
    {
        // Arrange — five hex digits used to miss the object pattern, so an unlisted
        // [00020] produced no OBJ011 even though the reader never loads index 0x20 from it.
        const string content = @"
[00020]
ParameterName=Custom
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
            f.Code == "OBJ011" &&
            f.Severity == Severity.Error &&
            f.Section == "00020" &&
            f.Message.Contains("[20]", StringComparison.Ordinal));
        findings.Should().Contain(f => f.Code == "LST003" && f.Section == "00020");
    }

    [Fact]
    public void Check_OverWidthAndCanonicalIndex_ReportsObj011AndIni002()
    {
        // Arrange
        const string content = @"
[20]
ParameterName=First
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[00020]
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
            f.Section == "00020" &&
            f.Message.Contains("[20]", StringComparison.Ordinal));
        findings.Should().Contain(f => f.Code == "OBJ011" && f.Section == "00020");
    }

    [Theory]
    [InlineData("10000")]
    [InlineData("00010000")]
    public void Check_IndexBeyondUInt16_DoesNotReportObj011(string sectionName)
    {
        // Arrange — zero-prefixed only counts when the text still parses as a ushort.
        var content = "[" + sectionName + @"]
ParameterName=TooWide
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0
";

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().NotContain(f => f.Code == "OBJ011");
        findings.Should().NotContain(f => f.Section == sectionName && f.Code == "LST003");
        findings.Should().NotContain(f => f.Section == sectionName && f.Code == "VAL001");
    }

    [Theory]
    [InlineData("20sub100")]
    [InlineData("10000sub1")]
    public void Check_SubIndexBeyondIntegerWidth_DoesNotReportObj011(string sectionName)
    {
        // Arrange — [20sub100] is above a byte; [10000sub1] is above a ushort.
        var content = "[" + sectionName + @"]
ParameterName=TooWide
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
        findings.Should().NotContain(f => f.Section == sectionName && f.Code == "OBJ008");
    }

    [Theory]
    [InlineData("1018sub01", "[1018sub1]")]
    [InlineData("1018sub0A", "[1018subA]")]
    [InlineData("1018sub00", "[1018sub0]")]
    [InlineData("40sub01", "[40sub1]")]
    [InlineData("0020sub1", "[20sub1]")]
    [InlineData("00020sub1", "[20sub1]")]
    [InlineData("20sub001", "[20sub1]")]
    [InlineData("00020sub001", "[20sub1]")]
    [InlineData("0040sub0", "[40sub0]")]
    [InlineData("020sub01", "[20sub1]")]
    [InlineData("0FFFFsubFF", "[FFFFsubFF]")]
    [InlineData("20sub0FF", "[20subFF]")]
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
    [InlineData("1018subFF")]
    [InlineData("40sub0")]
    [InlineData("20sub1")]
    [InlineData("Asub1")]
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

    [Theory]
    [InlineData("00020sub0")]
    [InlineData("20sub000")]
    [InlineData("00020sub000")]
    public void Check_PaddedSubBeforeCanonical_UsesTheSectionTheReaderLoads(string paddedName)
    {
        // Arrange — sub-index 0 is cross-checked only for the section kept in the
        // sub-object map. The reader loads [20sub0], which announces 9 while only
        // sub-index 1 exists. The padded alias announces 1 and must not hide that.
        var content = @"
[20]
ParameterName=Record
ObjectType=0x9
SubNumber=2

[" + paddedName + @"]
ParameterName=HighestPadded
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[20sub0]
ParameterName=HighestCanonical
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=9
PDOMapping=0

[20sub1]
ParameterName=Entry
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=0
";

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f => f.Code == "OBJ011" && f.Section == paddedName);
        findings.Should().Contain(f =>
            f.Code == "INI002" &&
            f.Section == "20sub0" &&
            f.Message.Contains("[" + paddedName + "]", StringComparison.Ordinal));
        findings.Should().Contain(f =>
            f.Code == "OBJ007" &&
            f.Section == "20sub0" &&
            f.Message.Contains("9", StringComparison.Ordinal));
        findings.Should().NotContain(f => f.Code == "OBJ007" && f.Section == paddedName);
    }

    [Fact]
    public void Check_CanonicalSubBeforePadded_KeepsTheCanonicalSection()
    {
        // Arrange — the first spelling is already the one the reader loads.
        const string content = @"
[20]
ParameterName=Record
ObjectType=0x9
SubNumber=2

[20sub0]
ParameterName=HighestCanonical
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=9
PDOMapping=0

[00020sub0]
ParameterName=HighestPadded
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[20sub1]
ParameterName=Entry
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=0
";

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f => f.Code == "OBJ007" && f.Section == "20sub0");
        findings.Should().NotContain(f => f.Code == "OBJ007" && f.Section == "00020sub0");
        findings.Should().Contain(f => f.Code == "INI002" && f.Section == "00020sub0");
    }

    [Fact]
    public void Check_TwoPaddedSubs_KeepsTheFirstForCrossChecks()
    {
        // Arrange — neither spelling is [20sub0]. The later padded alias must not
        // replace the first one in the sub-object map.
        const string content = @"
[20]
ParameterName=Record
ObjectType=0x9
SubNumber=2

[00020sub0]
ParameterName=FirstPadded
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=9
PDOMapping=0

[0020sub0]
ParameterName=SecondPadded
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[20sub1]
ParameterName=Entry
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=0
";

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f => f.Code == "OBJ007" && f.Section == "00020sub0");
        findings.Should().NotContain(f => f.Code == "OBJ007" && f.Section == "0020sub0");
    }

    [Fact]
    public void Check_PaddedSubBeforeCanonical_PdoMappingUsesCanonical()
    {
        // Arrange — [02000sub1] is not PDO-mappable. The reader loads [2000sub1], which is.
        const string content = @"
[1600]
ParameterName=RPDO
ObjectType=0x9
SubNumber=2

[1600sub0]
ParameterName=Number
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0

[1600sub1]
ParameterName=Map
ObjectType=0x7
DataType=0x0007
AccessType=rw
DefaultValue=0x20000108
PDOMapping=0

[2000]
ParameterName=Mapped
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0

[02000sub1]
ParameterName=Padded
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0

[2000sub1]
ParameterName=Canonical
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=1
";

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().Contain(f => f.Code == "OBJ011" && f.Section == "02000sub1");
        findings.Should().Contain(f =>
            f.Code == "INI002" &&
            f.Section == "2000sub1" &&
            f.Message.Contains("[02000sub1]", StringComparison.Ordinal));
        findings.Should().NotContain(f => f.Code == "PDO002");
    }

    [Fact]
    public void Check_PaddedSubBeforeCanonical_DcfOverrideUsesCanonicalLimits()
    {
        // Arrange — [20Value] entry 1=50 fits the canonical HighLimit and not the padded one.
        const string content = @"
[DeviceComissioning]
NodeID=1

[20]
ParameterName=Record
ObjectType=0x9
SubNumber=1

[00020sub1]
ParameterName=Padded
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=1
LowLimit=0
HighLimit=10
PDOMapping=0

[20sub1]
ParameterName=Canonical
ObjectType=0x7
DataType=0x0005
AccessType=rw
DefaultValue=1
LowLimit=0
HighLimit=100
PDOMapping=0

[20Value]
1=50
";

        // Act
        var findings = Check(content, isDcf: true);

        // Assert
        findings.Should().Contain(f => f.Code == "OBJ011" && f.Section == "00020sub1");
        findings.Should().Contain(f => f.Code == "INI002" && f.Section == "20sub1");
        findings.Should().NotContain(f => f.Code == "VAL004");
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
        findings.Should().NotContain(f => f.Code == "OBJ011");
    }

    [Theory]
    [InlineData("40", "0040Value", "[40Value]")]
    [InlineData("40", "040Value", "[40Value]")]
    [InlineData("40", "00040Value", "[40Value]")]
    [InlineData("A", "000AValue", "[AValue]")]
    [InlineData("1018", "01018Value", "[1018Value]")]
    [InlineData("FFFF", "0FFFFValue", "[FFFFValue]")]
    [InlineData("0", "00000Value", "[0Value]")]
    public void Check_PaddedCompactValue_ReportsObj011_AndDoesNotApplyIt(
        string objectSection,
        string valueSection,
        string readerName)
    {
        // Arrange — 999 does not fit UNSIGNED8. Accepting the padded section as a
        // fallback would report VAL001; the reader never applies it.
        var content = @"
[DeviceComissioning]
NodeID=1

[OptionalObjects]
SupportedObjects=1
1=0x" + objectSection + @"

[" + objectSection + @"]
ParameterName=Compact
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0
CompactSubObj=1

[" + valueSection + @"]
1=999
";

        // Act
        var findings = Check(content, isDcf: true);

        // Assert
        findings.Should().Contain(f =>
            f.Code == "OBJ011" &&
            f.Severity == Severity.Error &&
            f.Section == valueSection &&
            f.Message.Contains(readerName, StringComparison.Ordinal));
        findings.Should().NotContain(f => f.Code == "VAL001");
    }

    [Fact]
    public void Check_PaddedAndUnpaddedValue_ReportsObj011_AndChecksUnpadded()
    {
        // Arrange
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

[0040Value]
1=1
";

        // Act
        var findings = Check(content, isDcf: true);

        // Assert
        findings.Should().Contain(f => f.Code == "VAL001" && f.Section == "40Value");
        findings.Should().Contain(f =>
            f.Code == "OBJ011" &&
            f.Section == "0040Value" &&
            f.Message.Contains("[40Value]", StringComparison.Ordinal));
        findings.Should().NotContain(f => f.Code == "VAL001" && f.Section == "0040Value");
    }

    [Fact]
    public void Check_OverWidthObjectAndCompactValue_ReportsObj011ForBoth()
    {
        // Arrange — [00040] must be collected before the compact-value scan can see
        // [00040Value]. 999 does not fit UNSIGNED8 and must not be applied.
        const string content = @"
[DeviceComissioning]
NodeID=1

[OptionalObjects]
SupportedObjects=1
1=0x40

[00040]
ParameterName=Compact
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0
CompactSubObj=1

[00040Value]
1=999
";

        // Act
        var findings = Check(content, isDcf: true);

        // Assert
        findings.Should().Contain(f =>
            f.Code == "OBJ011" &&
            f.Severity == Severity.Error &&
            f.Section == "00040" &&
            f.Message.Contains("[40]", StringComparison.Ordinal));
        findings.Should().Contain(f =>
            f.Code == "OBJ011" &&
            f.Severity == Severity.Error &&
            f.Section == "00040Value" &&
            f.Message.Contains("[40Value]", StringComparison.Ordinal));
        findings.Should().NotContain(f => f.Code == "VAL001");
        findings.Should().NotContain(f => f.Code == "LST002");
    }

    [Fact]
    public void Check_CanonicalValueSection_DoesNotReportObj011()
    {
        // Arrange — 0x1018 is already four digits, so [1018Value] is the reader name.
        const string content = @"
[DeviceComissioning]
NodeID=1

[1018]
ParameterName=Identity
ObjectType=0x8
DataType=0x0007
AccessType=ro
DefaultValue=1
PDOMapping=0
CompactSubObj=1

[1018Value]
1=1
";

        // Act
        var findings = Check(content, isDcf: true);

        // Assert
        findings.Should().NotContain(f => f.Code == "OBJ011");
        findings.Should().NotContain(f => f.Code == "VAL001");
    }

    [Fact]
    public void Check_ValueSuffixCase_IsTheCanonicalSection()
    {
        // Arrange — section lookup is case-insensitive, so [40value] is [40Value].
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

[40value]
1=999
";

        // Act
        var findings = Check(content, isDcf: true);

        // Assert
        findings.Should().Contain(f => f.Code == "VAL001" && f.Section == "40value");
        findings.Should().NotContain(f => f.Code == "OBJ011");
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

    [Fact]
    public void ReadString_PaddedObjectIndex_ReaderDropsIt_UnpaddedIsLoaded()
    {
        // Arrange
        const string padded = @"
[DeviceInfo]
VendorName=Test

[OptionalObjects]
SupportedObjects=1
1=0x20

[0020]
ParameterName=Custom
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0
";
        const string unpadded = @"
[DeviceInfo]
VendorName=Test

[OptionalObjects]
SupportedObjects=1
1=0x20

[20]
ParameterName=Custom
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0
";

        // Act
        var dropped = CanOpenFile.Eds.ReadString(padded);
        var loaded = CanOpenFile.Eds.ReadString(unpadded);

        // Assert
        dropped.ObjectDictionary.Objects.Should().NotContainKey(0x20);
        loaded.ObjectDictionary.Objects.Should().ContainKey(0x20);
        loaded.ObjectDictionary.Objects[0x20].ParameterName.Should().Be("Custom");
    }

    [Fact]
    public void ReadString_PaddedBeforeCanonical_ReaderLoadsCanonical()
    {
        // Arrange — ParseObject probes [40] only, whichever spelling appears first.
        const string content = @"
[DeviceInfo]
VendorName=Test

[OptionalObjects]
SupportedObjects=1
1=0x40

[0040]
ParameterName=Padded
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[40]
ParameterName=Canonical
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0
";

        // Act
        var loaded = CanOpenFile.Eds.ReadString(content);

        // Assert
        loaded.ObjectDictionary.Objects.Should().ContainKey(0x40);
        loaded.ObjectDictionary.Objects[0x40].ParameterName.Should().Be("Canonical");
    }

    [Fact]
    public void ReadString_OverWidthPaddedObjectIndex_ReaderDropsIt()
    {
        // Arrange — [00020] is the same index as [20], with one extra leading zero.
        const string content = @"
[DeviceInfo]
VendorName=Test

[OptionalObjects]
SupportedObjects=1
1=0x20

[00020]
ParameterName=Custom
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0
";

        // Act
        var dropped = CanOpenFile.Eds.ReadString(content);

        // Assert
        dropped.ObjectDictionary.Objects.Should().NotContainKey(0x20);
    }

    [Fact]
    public void ReadString_OverWidthPaddedIndexAtMaxValue_ReaderDropsIt()
    {
        // Arrange — 0xFFFF is the largest object index. [0FFFF] is not [FFFF].
        const string padded = @"
[DeviceInfo]
VendorName=Test

[OptionalObjects]
SupportedObjects=1
1=0xFFFF

[0FFFF]
ParameterName=Custom
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0
";
        const string unpadded = @"
[DeviceInfo]
VendorName=Test

[OptionalObjects]
SupportedObjects=1
1=0xFFFF

[FFFF]
ParameterName=Custom
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0
";

        // Act
        var dropped = CanOpenFile.Eds.ReadString(padded);
        var loaded = CanOpenFile.Eds.ReadString(unpadded);

        // Assert
        dropped.ObjectDictionary.Objects.Should().NotContainKey(ushort.MaxValue);
        loaded.ObjectDictionary.Objects.Should().ContainKey(ushort.MaxValue);
        loaded.ObjectDictionary.Objects[ushort.MaxValue].ParameterName.Should().Be("Custom");
    }

    [Fact]
    public void ReadString_OverWidthPaddedSubIndex_ReaderDropsIt()
    {
        // Arrange
        const string content = @"
[DeviceInfo]
VendorName=Test

[OptionalObjects]
SupportedObjects=1
1=0x20

[20]
ParameterName=Parent
ObjectType=0x9
SubNumber=1

[00020sub1]
ParameterName=Child
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0
";

        // Act
        var dropped = CanOpenFile.Eds.ReadString(content);

        // Assert
        dropped.ObjectDictionary.Objects.Should().ContainKey(0x20);
        dropped.ObjectDictionary.Objects[0x20].SubObjects.Should().BeEmpty();
    }

    [Fact]
    public void ReadString_PaddedCompactValue_ReaderIgnoresIt_UnpaddedIsApplied()
    {
        // Arrange
        const string padded = @"
[DeviceInfo]
VendorName=Test

[DeviceComissioning]
NodeID=1

[OptionalObjects]
SupportedObjects=1
1=0x40

[40]
ParameterName=Compact
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0
CompactSubObj=1

[0040Value]
1=7
";
        const string unpadded = @"
[DeviceInfo]
VendorName=Test

[DeviceComissioning]
NodeID=1

[OptionalObjects]
SupportedObjects=1
1=0x40

[40]
ParameterName=Compact
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0
CompactSubObj=1

[40Value]
1=7
";

        // Act
        var dropped = CanOpenFile.Dcf.ReadString(padded);
        var loaded = CanOpenFile.Dcf.ReadString(unpadded);

        // Assert
        dropped.ObjectDictionary.Objects[0x40].SubObjects[1].ParameterValue.Should().BeNull();
        loaded.ObjectDictionary.Objects[0x40].SubObjects[1].ParameterValue.Should().Be("7");
    }

    [Theory]
    [InlineData("0040Name", "[40Name]")]
    [InlineData("00040Name", "[40Name]")]
    [InlineData("0040ObjectLinks", "[40ObjectLinks]")]
    [InlineData("00040ObjectLinks", "[40ObjectLinks]")]
    public void Check_PaddedEdsAuxiliarySection_ReportsObj011(string sectionName, string readerName)
    {
        // Arrange — compact name and object-link sections are probed on EDS too.
        var content = @"
[OptionalObjects]
SupportedObjects=1
1=0x40

[40]
ParameterName=Compact
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0
CompactSubObj=1

[" + sectionName + @"]
1=Custom
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

    [Fact]
    public void Check_PaddedNameWithoutCompactSubObj_DoesNotReportObj011()
    {
        // Arrange — without CompactSubObj the reader keeps the name section.
        const string content = @"
[40]
ParameterName=Var
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[0040Name]
1=Custom
";

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().NotContain(f => f.Code == "OBJ011" && f.Section == "0040Name");
    }

    [Fact]
    public void Check_EdsPaddedValueAndDenotation_DoNotReportObj011()
    {
        // Arrange — EDS does not construct [xxxxValue] or [xxxxDenotation].
        const string content = @"
[40]
ParameterName=Compact
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0
CompactSubObj=1

[0040Value]
1=7

[0040Denotation]
1=Label
";

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().NotContain(f => f.Section == "0040Value" && f.Code == "OBJ011");
        findings.Should().NotContain(f => f.Section == "0040Denotation" && f.Code == "OBJ011");
    }

    [Theory]
    [InlineData("0040Denotation", "[40Denotation]")]
    [InlineData("00040Denotation", "[40Denotation]")]
    [InlineData("0040ObjectLinks", "[40ObjectLinks]")]
    public void Check_PaddedDcfAuxiliarySection_ReportsObj011(string sectionName, string readerName)
    {
        // Arrange
        var content = @"
[DeviceComissioning]
NodeID=1

[OptionalObjects]
SupportedObjects=1
1=0x40

[40]
ParameterName=Compact
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=1
PDOMapping=0
CompactSubObj=1

[" + sectionName + @"]
1=Custom
";

        // Act
        var findings = Check(content, isDcf: true);

        // Assert
        findings.Should().Contain(f =>
            f.Code == "OBJ011" &&
            f.Severity == Severity.Error &&
            f.Section == sectionName &&
            f.Message.Contains(readerName, StringComparison.Ordinal));
    }

    [Fact]
    public void Check_CanonicalNameAndObjectLinks_DoNotReportObj011()
    {
        // Arrange
        const string content = @"
[40]
ParameterName=Compact
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0
CompactSubObj=1

[40Name]
1=Custom

[40ObjectLinks]
ObjectLinks=1
1=0x1000
";

        // Act
        var findings = Check(content);

        // Assert
        findings.Should().NotContain(f => f.Code == "OBJ011");
    }

    [Fact]
    public void ReadString_PaddedCompactName_ReaderDropsIt_UnpaddedIsApplied()
    {
        // Arrange
        const string padded = @"
[DeviceInfo]
VendorName=Test

[OptionalObjects]
SupportedObjects=1
1=0x40

[40]
ParameterName=Compact
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=0
CompactSubObj=1

[0040Name]
1=Custom
";
        const string unpadded = @"
[DeviceInfo]
VendorName=Test

[OptionalObjects]
SupportedObjects=1
1=0x40

[40]
ParameterName=Compact
ObjectType=0x8
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=0
CompactSubObj=1

[40Name]
1=Custom
";

        // Act
        var dropped = CanOpenFile.Eds.ReadString(padded);
        var loaded = CanOpenFile.Eds.ReadString(unpadded);

        // Assert
        dropped.ObjectDictionary.Objects[0x40].SubObjects[1].ParameterName.Should().Be("Compact1");
        dropped.AdditionalSections.Should().NotContainKey("0040Name");
        loaded.ObjectDictionary.Objects[0x40].SubObjects[1].ParameterName.Should().Be("Custom");
    }

    [Fact]
    public void ReadString_PaddedDenotation_ReaderDropsIt_UnpaddedIsApplied()
    {
        // Arrange
        const string padded = @"
[DeviceInfo]
VendorName=Test

[DeviceComissioning]
NodeID=1

[OptionalObjects]
SupportedObjects=1
1=0x40

[40]
ParameterName=Compact
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0
CompactSubObj=1

[0040Denotation]
1=Label
";
        const string unpadded = @"
[DeviceInfo]
VendorName=Test

[DeviceComissioning]
NodeID=1

[OptionalObjects]
SupportedObjects=1
1=0x40

[40]
ParameterName=Compact
ObjectType=0x8
DataType=0x0005
AccessType=rw
DefaultValue=0
PDOMapping=0
CompactSubObj=1

[40Denotation]
1=Label
";

        // Act
        var dropped = CanOpenFile.Dcf.ReadString(padded);
        var loaded = CanOpenFile.Dcf.ReadString(unpadded);

        // Assert
        dropped.ObjectDictionary.Objects[0x40].SubObjects[1].Denotation.Should().BeNull();
        dropped.AdditionalSections.Should().NotContainKey("0040Denotation");
        loaded.ObjectDictionary.Objects[0x40].SubObjects[1].Denotation.Should().Be("Label");
    }

    [Fact]
    public void ReadString_PaddedObjectLinks_ReaderDropsIt_UnpaddedIsApplied()
    {
        // Arrange
        const string padded = @"
[DeviceInfo]
VendorName=Test

[OptionalObjects]
SupportedObjects=1
1=0x40

[40]
ParameterName=Compact
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[0040ObjectLinks]
ObjectLinks=1
1=0x1000
";
        const string unpadded = @"
[DeviceInfo]
VendorName=Test

[OptionalObjects]
SupportedObjects=1
1=0x40

[40]
ParameterName=Compact
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=1
PDOMapping=0

[40ObjectLinks]
ObjectLinks=1
1=0x1000
";

        // Act
        var dropped = CanOpenFile.Eds.ReadString(padded);
        var loaded = CanOpenFile.Eds.ReadString(unpadded);

        // Assert
        dropped.ObjectDictionary.Objects[0x40].ObjectLinks.Should().BeEmpty();
        loaded.ObjectDictionary.Objects[0x40].ObjectLinks.Should().Equal((ushort)0x1000);
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
