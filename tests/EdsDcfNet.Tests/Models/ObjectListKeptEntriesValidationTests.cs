namespace EdsDcfNet.Tests.Models;

using EdsDcfNet.Diagnostics;
using EdsDcfNet.Models;
using EdsDcfNet.Validation;

/// <summary>
/// <see cref="CanOpenValidationOptions.CheckObjectListEntries"/>: a numbered entry of
/// <c>[MandatoryObjects]</c>, <c>[OptionalObjects]</c> or <c>[ManufacturerObjects]</c> that the
/// reader keeps in <c>SectionRemainingEntries</c> is either written after the generated list,
/// above <c>SupportedObjects</c> (CiA 306-1 Table 5), or replaced by a generated slot. The opt-in
/// rule reports both; default validation and validated writes are unchanged.
/// </summary>
public class ObjectListKeptEntriesValidationTests
{
    private static readonly CanOpenValidationOptions EntriesOnly = new() { CheckObjectListEntries = true };

    private const string AboveCount =
        "[DeviceInfo]\nVendorName=Test\nProductName=Test\n\n"
        + "[MandatoryObjects]\nSupportedObjects=1\n1=0x1000\n2=0x2000\n\n"
        + "[1000]\nParameterName=Device Type\nObjectType=0x7\nDataType=0x0007\nAccessType=ro\nDefaultValue=0\nPDOMapping=0\n\n"
        + "[2000]\nParameterName=Hidden\nObjectType=0x7\nDataType=0x0005\nAccessType=rw\nPDOMapping=0\n";

    [Fact]
    public void Validate_KeptEntryAboveSupportedObjects_OptInReportsEntry()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(AboveCount);

        // Act
        var issues = CanOpenFile.Validate(eds, EntriesOnly);

        // Assert
        issues.Should().ContainSingle(issue => issue.Code == ValidationIssueCodes.IniObjectListExtraEntry)
            .Which.Should().Match<ValidationIssue>(issue =>
                issue.Path == "SectionRemainingEntries[MandatoryObjects][2]" &&
                issue.Message.Contains("SupportedObjects=1", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_KeptEntryAboveSupportedObjects_DefaultOptionsReportNothing()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(AboveCount);

        // Act
        var issues = CanOpenFile.Validate(eds);

        // Assert
        issues.Should().NotContain(issue => issue.Code == ValidationIssueCodes.IniObjectListExtraEntry);
    }

    [Fact]
    public void Validate_KeptEntryAboveSupportedObjects_StrictOptionsReportEntry()
    {
        // Arrange
        var dcf = CanOpenFile.Dcf.ReadString(AboveCount);

        // Act
        var issues = CanOpenFile.Validate(dcf, CanOpenValidationOptions.Strict);

        // Assert
        issues.Should().Contain(issue =>
            issue.Code == ValidationIssueCodes.IniObjectListExtraEntry &&
            issue.Path == "SectionRemainingEntries[MandatoryObjects][2]");
    }

    [Fact]
    public void WriteToString_KeptEntryAboveSupportedObjects_ValidatedWriteUnchanged()
    {
        // Arrange — CanOpenWriteOptions.Validated uses the default rule sets.
        var eds = CanOpenFile.Eds.ReadString(AboveCount);

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        written.Should().Contain("SupportedObjects=1").And.Contain("2=0x2000");
    }

    [Fact]
    public void Validate_KeptEntryInsideWrittenCount_OptInReportsReplacedSlot()
    {
        // Arrange — slot 1 is empty, so the read list holds only 0x1001; the writer generates
        // SupportedObjects=1 and 1=0x1001, and the kept empty "1=" is replaced, "2=" stays above.
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.OptionalObjects.Add(0x1001);
        eds.ObjectDictionary.Objects[0x1001] = new CanOpenObject
        {
            Index = 0x1001,
            ParameterName = "Error Register",
            ObjectType = CanOpenObjectType.Var,
            DataType = CanOpenDataType.Unsigned8
        };
        var kept = new OrderedStringDictionary { { "1", "" }, { "7", "0x1003" }, { "01", "x" }, { "Vendor", "y" } };
        eds.SectionRemainingEntries["OptionalObjects"] = kept;

        // Act
        var issues = CanOpenFile.Validate(eds, EntriesOnly)
            .Where(issue => issue.Code == ValidationIssueCodes.IniObjectListExtraEntry)
            .ToList();

        // Assert
        issues.Select(issue => issue.Path).Should().Equal(
            "SectionRemainingEntries[OptionalObjects][1]",
            "SectionRemainingEntries[OptionalObjects][7]");
        issues[0].Message.Should().Contain("replaced by the generated entry");
        issues[1].Message.Should().Contain("above SupportedObjects=1");
    }

    [Fact]
    public void Validate_KeptEntryOfEveryList_OptInReportsEachList()
    {
        // Arrange — an empty list writes SupportedObjects=0, so every numbered kept entry is above it.
        var dcf = new DeviceConfigurationFile();
        dcf.SectionRemainingEntries["MandatoryObjects"] = new OrderedStringDictionary { { "1", "0x1000" } };
        dcf.SectionRemainingEntries["optionalobjects"] = new OrderedStringDictionary { { "1", "0x1001" } };
        dcf.SectionRemainingEntries["ManufacturerObjects"] = new OrderedStringDictionary { { "3", "0x2000" } };
        dcf.SectionRemainingEntries["DummyUsage"] = new OrderedStringDictionary { { "1", "x" } };

        // Act
        var issues = CanOpenFile.Validate(dcf, EntriesOnly)
            .Where(issue => issue.Code == ValidationIssueCodes.IniObjectListExtraEntry)
            .Select(issue => issue.Path);

        // Assert
        issues.Should().BeEquivalentTo(
            "SectionRemainingEntries[MandatoryObjects][1]",
            "SectionRemainingEntries[OptionalObjects][1]",
            "SectionRemainingEntries[ManufacturerObjects][3]");
    }

    [Fact]
    public void Validate_NoKeptListEntries_OptInReportsNothing()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(AboveCount.Replace("2=0x2000\n", string.Empty).Replace(
            "[2000]\nParameterName=Hidden\nObjectType=0x7\nDataType=0x0005\nAccessType=rw\nPDOMapping=0\n",
            string.Empty));

        // Act
        var issues = CanOpenFile.Validate(eds, EntriesOnly);

        // Assert
        issues.Should().NotContain(issue => issue.Code == ValidationIssueCodes.IniObjectListExtraEntry);
    }

    [Fact]
    public void Validate_NullObjectListStore_OptInReportsNothing()
    {
        // Arrange — a null store means nothing is kept (the writer skips it as well).
        var eds = CanOpenFile.Eds.ReadString(AboveCount);
        eds.SectionRemainingEntries["MandatoryObjects"] = null!;

        // Act
        var issues = CanOpenFile.Validate(eds, EntriesOnly);

        // Assert
        issues.Should().NotContain(issue => issue.Code == ValidationIssueCodes.IniObjectListExtraEntry);
    }

    [Fact]
    public async Task ValidateAsync_KeptEntryAboveSupportedObjects_OptInReportsEntry()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(AboveCount);

        // Act
        var issues = await CanOpenFile.ValidateAsync(eds, EntriesOnly, CancellationToken.None);

        // Assert
        issues.Should().ContainSingle(issue => issue.Code == ValidationIssueCodes.IniObjectListExtraEntry);
    }

    [Fact]
    public void ApplyKeptObjectListEntries_CanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange — called directly: the public async entry points already observe a token that
        // is canceled before validation starts.
        var eds = CanOpenFile.Eds.ReadString(AboveCount);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        var act = () => IniWriteRules.ApplyKeptObjectListEntries(eds, new List<ValidationIssue>(), cancellation.Token);

        // Assert
        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public async Task ValidateAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(AboveCount);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        var act = () => CanOpenFile.ValidateAsync(eds, EntriesOnly, cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void ReadString_KeptEntryAboveSupportedObjects_ReaderStillReportsCountMismatch()
    {
        // Arrange / Act
        var result = CanOpenFile.Eds.ReadStringWithDiagnostics(AboveCount);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Code == ParseDiagnosticCodes.IniObjectListCountMismatch);
        result.Model.SectionRemainingEntries["MandatoryObjects"]["2"].Should().Be("0x2000");
    }
}
