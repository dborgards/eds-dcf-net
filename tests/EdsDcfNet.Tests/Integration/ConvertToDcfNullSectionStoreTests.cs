namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Models;

/// <summary>
/// A <see langword="null"/> value in <c>SectionRemainingEntries</c> means "nothing kept". The
/// writer and the validation rules already ignore it; the conversion must do the same.
/// </summary>
public class ConvertToDcfNullSectionStoreTests
{
    [Fact]
    public void ConvertToDcf_NullSectionStore_IsSkipped()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(string.Join(
            "\n",
            "[FileInfo]",
            "FileName=device.eds",
            "",
            "[DeviceInfo]",
            "VendorName=Vendor",
            "",
            "[MandatoryObjects]",
            "SupportedObjects=1",
            "1=0x1000",
            "Vendor=kept",
            "",
            "[1000]",
            "ParameterName=Device type",
            "ObjectType=0x7",
            "DataType=0x0007",
            "AccessType=ro",
            "DefaultValue=0",
            "PDOMapping=0",
            "") + "\n");
        eds.SectionRemainingEntries["Tools"] = null!;

        // Act
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 5, timestamp: new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc));

        // Assert — like the writer, a null store keeps nothing, so the DCF has no entry for it.
        dcf.SectionRemainingEntries.Should().NotContainKey("Tools");
        dcf.SectionRemainingEntries["MandatoryObjects"].Should().Equal(new Dictionary<string, string> { ["Vendor"] = "kept" });
        CanOpenFile.Dcf.WriteToString(dcf).Should().Contain("Vendor=kept");
    }
}
