namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Validation;

/// <summary>
/// <c>[DeviceComissioning]</c> (CiA 306-1 v1.4.0 § 7.3.5) that holds only kept entries is still
/// written; the kept entries alone make the section necessary.
/// </summary>
public class CommissioningRemainingEntriesTests
{
    private static readonly string Base = string.Join(
        "\n",
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
        "") + "\n";

    [Fact]
    public void WriteString_CommissioningSectionWithOnlyVendorKey_KeepsSection()
    {
        // Arrange — a section read from a file gets the NodeID/Baudrate defaults.
        var content = Base + "[DeviceComissioning]\nVendor=X\n";

        // Act
        var written = CanOpenFile.Dcf.WriteToString(CanOpenFile.Dcf.ReadString(content));
        var again = CanOpenFile.Dcf.ReadString(written);

        // Assert
        again.DeviceCommissioning.RemainingEntries["Vendor"].Should().Be("X");
    }

    [Fact]
    public void WriteString_OmittedCommissioningWithKeptEntry_WritesSection()
    {
        // Arrange — every commissioning property is unset, but a kept entry exists.
        var dcf = CanOpenFile.Dcf.ReadString(Base);
        dcf.DeviceCommissioning.RemainingEntries["Vendor"] = "X";

        // Act
        var act = () => CanOpenFile.Dcf.WriteToString(dcf);

        // Assert — the section is required, so the out-of-range NodeId 0 is reported instead of
        // the kept entry being dropped silently.
        act.Should().Throw<DcfWriteException>().WithMessage("*NodeId 0*");
    }

    [Fact]
    public void WriteString_OmittedCommissioningWithOnlyDedicatedKeptKey_OmitsSection()
    {
        // Arrange — the writer never outputs a kept NodeID (it writes the property instead),
        // so this entry alone does not require the section.
        var dcf = CanOpenFile.Dcf.ReadString(Base);
        dcf.DeviceCommissioning.RemainingEntries["NodeID"] = "5";

        // Act
        var written = CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        written.Should().NotContain("[DeviceComissioning]");
    }

    [Fact]
    public void IniWriteRules_OmittedCommissioningWithOnlyDedicatedKeptKey_IsNotChecked()
    {
        // Arrange
        var dcf = CanOpenFile.Dcf.ReadString(Base);
        dcf.DeviceCommissioning.RemainingEntries["NodeID"] = "bad\nvalue";
        var issues = new List<ValidationIssue>();

        // Act
        IniWriteRules.Apply(dcf, issues);

        // Assert
        issues.Should().NotContain(issue => issue.Path.StartsWith("DeviceCommissioning", StringComparison.Ordinal));
    }

    [Fact]
    public void IniWriteRules_OmittedCommissioningWithKeptEntry_ChecksKeptEntry()
    {
        // Arrange
        var dcf = CanOpenFile.Dcf.ReadString(Base);
        dcf.DeviceCommissioning.RemainingEntries["Bad=Key"] = "X";
        var issues = new List<ValidationIssue>();

        // Act
        IniWriteRules.Apply(dcf, issues);

        // Assert
        issues.Should().Contain(issue => issue.Path == "DeviceCommissioning.RemainingEntries[Bad=Key]");
    }

    [Fact]
    public void IniWriteRules_OmittedCommissioningWithoutKeptEntries_IsNotChecked()
    {
        // Arrange
        var dcf = CanOpenFile.Dcf.ReadString(Base);
        dcf.DeviceCommissioning = new DeviceCommissioning();
        var issues = new List<ValidationIssue>();

        // Act
        IniWriteRules.Apply(dcf, issues);

        // Assert
        issues.Should().NotContain(issue => issue.Path.StartsWith("DeviceCommissioning", StringComparison.Ordinal));
    }
}
