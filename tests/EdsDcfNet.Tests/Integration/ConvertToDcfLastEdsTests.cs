namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;
using EdsDcfNet.Models;

/// <summary>
/// <c>ConvertToDcf</c> sets <c>LastEDS</c> (CiA 306-1 v1.4.0 § 7.2) from the EDS file name. An
/// EDS without <c>FileName</c> may still carry a kept <c>LastEDS</c> entry; that value must not
/// be lost.
/// </summary>
public class ConvertToDcfLastEdsTests
{
    private static readonly DateTime Timestamp = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ConvertToDcf_NoFileNameButKeptLastEds_AdoptsKeptValue()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Fixture("lasteds=old.eds"));

        // Act
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 5, timestamp: Timestamp);
        AvoidTrailingSpaceInGeneratedDescription(dcf);
        var written = CanOpenFile.Dcf.WriteToString(dcf);

        // Assert
        dcf.FileInfo.LastEds.Should().Be("old.eds");
        dcf.FileInfo.RemainingEntries.Should().NotContainKey("LastEDS");
        CountLines(written, "LastEDS=").Should().Be(1);
        written.Should().Contain("LastEDS=old.eds");
    }

    [Fact]
    public void ConvertToDcf_NoFileNameAndEmptyKeptLastEds_KeepsEmptyEntry()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Fixture("LastEDS="));

        // Act
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 5, timestamp: Timestamp);
        AvoidTrailingSpaceInGeneratedDescription(dcf);
        var written = CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert — an empty value is no LastEds, so the kept entry is written back as it was.
        dcf.FileInfo.LastEds.Should().BeEmpty();
        dcf.FileInfo.RemainingEntries["LastEDS"].Should().BeEmpty();
        CountLines(written, "LastEDS=").Should().Be(1);
    }

    [Fact]
    public void ConvertToDcf_FileNameAndKeptLastEds_FileNameWins()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Fixture("FileName=device.eds", "LastEDS=old.eds"));

        // Act
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 5, timestamp: Timestamp);
        var written = CanOpenFile.Dcf.WriteToString(dcf);

        // Assert
        dcf.FileInfo.LastEds.Should().Be("device.eds");
        CountLines(written, "LastEDS=").Should().Be(1);
        written.Should().Contain("LastEDS=device.eds");
    }

    [Fact]
    public void ConvertToDcf_NoFileNameAndNoKeptLastEds_WritesNoLastEds()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Fixture("Vendor=file"));

        // Act
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 5, timestamp: Timestamp);
        AvoidTrailingSpaceInGeneratedDescription(dcf);
        var written = CanOpenFile.Dcf.WriteToString(dcf);

        // Assert
        dcf.FileInfo.LastEds.Should().BeEmpty();
        CountLines(written, "LastEDS=").Should().Be(0);
        written.Should().Contain("Vendor=file");
    }

    /// <summary>
    /// Without <c>FileName</c>, ConvertToDcf generates <c>Description="DCF generated from "</c>.
    /// The trailing space makes the writer reject the file. That is a separate issue; these
    /// tests replace the description so they test only <c>LastEDS</c>.
    /// </summary>
    private static void AvoidTrailingSpaceInGeneratedDescription(DeviceConfigurationFile dcf)
        => dcf.FileInfo.Description = "converted";

    private static string Fixture(params string[] fileInfoLines)
        => string.Join(
            "\n",
            new[] { "[FileInfo]" }
                .Concat(fileInfoLines)
                .Concat(new[]
                {
                    "",
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
                    "",
                })) + "\n";

    private static int CountLines(string ini, string prefix)
        => ini.Replace("\r\n", "\n").Split('\n').Count(line => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
