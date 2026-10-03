namespace EdsDcfNet.Tests.Writers;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Validation;

/// <summary>
/// The validated EDS/DCF write checks exactly the kept <c>SectionRemainingEntries</c> the
/// writer outputs: not a kept key the writer generates itself, and not a section the writer
/// does not emit.
/// </summary>
public class WrittenRemainingEntriesRuleTests
{
    private const string BadValue = "line\nbreak";

    // Built from single lines so the fixtures do not depend on the checkout's line endings.
    private static readonly string Eds = Ini(
        "[DeviceInfo]",
        "VendorName=Vendor",
        "",
        "[MandatoryObjects]",
        "SupportedObjects=1",
        "1=0x1000",
        "",
        "[OptionalObjects]",
        "SupportedObjects=1",
        "1=0x1018",
        "",
        "[ManufacturerObjects]",
        "SupportedObjects=1",
        "1=0x2000",
        "",
        "[1000]",
        "ParameterName=Device type",
        "ObjectType=0x7",
        "DataType=0x0007",
        "AccessType=ro",
        "DefaultValue=0",
        "PDOMapping=0",
        "",
        "[1018]",
        "ParameterName=Identity",
        "ObjectType=0x7",
        "DataType=0x0007",
        "AccessType=ro",
        "DefaultValue=0",
        "PDOMapping=0",
        "",
        "[2000]",
        "ParameterName=Compact",
        "ObjectType=0x8",
        "DataType=0x0007",
        "AccessType=rw",
        "DefaultValue=0",
        "PDOMapping=0",
        "CompactSubObj=1",
        "",
        "[2000Name]",
        "NrOfEntries=1",
        "1=First",
        "",
        "[2000ObjectLinks]",
        "ObjectLinks=1",
        "1=0x1000",
        "",
        "[SupportedModules]",
        "NrOfEntries=1",
        "",
        "[M1ModuleInfo]",
        "ProductName=Module",
        "ProductVersion=1",
        "ProductRevision=0",
        "OrderCode=M-1",
        "",
        "[M1FixedObjects]",
        "NrOfEntries=1",
        "1=0x6423",
        "",
        "[M1SubExtends]",
        "NrOfEntries=1",
        "1=0x6000",
        "",
        "[M1SubExt6000]",
        "ParameterName=Extension",
        "DataType=0x0005",
        "AccessType=ro",
        "PDOMapping=0",
        "Count=1",
        "");

    private static readonly string Simple = Ini(
        "[DeviceInfo]",
        "VendorName=Vendor",
        "",
        "[MandatoryObjects]",
        "SupportedObjects=1",
        "1=0x1000",
        "",
        "[1000]",
        "ParameterName=Device type",
        "ObjectType=0x7",
        "DataType=0x0007",
        "AccessType=ro",
        "DefaultValue=0",
        "PDOMapping=0",
        "");

    private static readonly string Dcf = Eds + Ini(
        "[DeviceComissioning]",
        "NodeID=2",
        "NodeName=Node",
        "Baudrate=250",
        "NetNumber=1",
        "NetworkName=Net",
        "CANopenManager=0",
        "",
        "[2000Value]",
        "NrOfEntries=1",
        "1=5",
        "",
        "[2000Denotation]",
        "NrOfEntries=1",
        "1=Den",
        "",
        "[ConnectedModules]",
        "NrOfEntries=1",
        "1=1",
        "");

    [Theory]
    // Generated or dedicated keys are suppressed by the writer and not checked.
    [InlineData(false, "DummyUsage", "Dummy0001", false)]
    [InlineData(false, "MandatoryObjects", "1", false)]
    [InlineData(false, "OptionalObjects", "SupportedObjects", false)]
    [InlineData(false, "ManufacturerObjects", "1", false)]
    [InlineData(false, "SupportedModules", "NrOfEntries", false)]
    [InlineData(false, "Tools", "Items", false)]
    [InlineData(false, "M1ModuleInfo", "ProductName", false)]
    [InlineData(false, "M1FixedObjects", "1", false)]
    [InlineData(false, "M1SubExtends", "1", false)]
    [InlineData(false, "M1SubExt6000", "Count", false)]
    [InlineData(false, "2000ObjectLinks", "1", false)]
    [InlineData(false, "2000Name", "1", false)]
    [InlineData(true, "ConnectedModules", "1", false)]
    [InlineData(true, "2000Value", "1", false)]
    [InlineData(true, "2000Denotation", "NrOfEntries", false)]
    // Sections the writer does not emit are not checked.
    [InlineData(false, "ConnectedModules", "Vendor", false)]
    [InlineData(false, "2000Value", "Vendor", false)]
    [InlineData(false, "M2ModuleInfo", "Vendor", false)]
    [InlineData(false, "M1SubExt6001", "Vendor", false)]
    [InlineData(false, "M01ModuleInfo", "Vendor", false)]
    [InlineData(false, "1000Name", "Vendor", false)]
    [InlineData(false, "3000ObjectLinks", "Vendor", false)]
    [InlineData(false, "Unrelated", "Vendor", false)]
    // Kept entries the writer outputs are checked.
    [InlineData(false, "DummyUsage", "Vendor", true)]
    [InlineData(false, "MandatoryObjects", "2", true)]
    [InlineData(false, "OptionalObjects", "Vendor", true)]
    [InlineData(false, "ManufacturerObjects", "Vendor", true)]
    [InlineData(false, "SupportedModules", "Vendor", true)]
    [InlineData(false, "Tools", "Vendor", true)]
    [InlineData(false, "m1moduleinfo", "Vendor", true)]
    [InlineData(false, "M1FixedObjects", "Vendor", true)]
    [InlineData(false, "M1SubExtends", "Vendor", true)]
    [InlineData(false, "M1SubExt6000", "Vendor", true)]
    [InlineData(false, "2000ObjectLinks", "Vendor", true)]
    [InlineData(false, "2000Name", "Vendor", true)]
    [InlineData(true, "ConnectedModules", "Vendor", true)]
    [InlineData(true, "2000Value", "Vendor", true)]
    [InlineData(true, "2000Denotation", "Vendor", true)]
    [InlineData(true, "1000Value", "Vendor", true)]
    public void Apply_KeptEntry_IsCheckedOnlyWhenWritten(bool isDcf, string section, string key, bool expectedIssue)
    {
        // Arrange
        object model = isDcf ? CanOpenFile.Dcf.ReadString(Dcf) : CanOpenFile.Eds.ReadString(Eds);
        var store = isDcf
            ? ((DeviceConfigurationFile)model).SectionRemainingEntries
            : ((ElectronicDataSheet)model).SectionRemainingEntries;
        store[section] = new OrderedStringDictionary { [key] = BadValue };
        var issues = new List<ValidationIssue>();

        // Act
        IniWriteRules.Apply(model, issues);

        // Assert
        issues.Any(issue => issue.Path.StartsWith("SectionRemainingEntries[", StringComparison.Ordinal))
            .Should().Be(expectedIssue);
    }

    [Fact]
    public void WriteToString_ValidatedRoundTrip_KeptEntryForGeneratedSlotDoesNotThrow()
    {
        // Arrange — slot 1 is generated from the object list, so the kept value is never written.
        var eds = CanOpenFile.Eds.ReadString(Simple);
        eds.SectionRemainingEntries["MandatoryObjects"] = new OrderedStringDictionary { ["1"] = BadValue };

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        written.Should().Contain("1=0x1000").And.NotContain("line");
    }

    [Fact]
    public void WriteToString_ValidatedRoundTrip_KeptEntryThatIsWrittenStillThrows()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Simple);
        eds.SectionRemainingEntries["MandatoryObjects"] = new OrderedStringDictionary { ["2"] = BadValue };

        // Act
        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        act.Should().Throw<ModelValidationException>()
            .Which.Issues.Should().Contain(issue => issue.Path == "SectionRemainingEntries[MandatoryObjects][2]");
    }

    private static string Ini(params string[] lines) => string.Join("\n", lines) + "\n";
}
