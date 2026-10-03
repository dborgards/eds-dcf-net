namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet;

/// <summary>
/// <c>ConvertToDcf</c> generates the <c>Description</c> from the EDS file name. An EDS without
/// <c>FileName</c> must still convert to a DCF that the writer accepts.
/// </summary>
public class ConvertToDcfDescriptionTests
{
    private static readonly DateTime Timestamp = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConvertToDcf_NoFileName_WritesWithoutWhitespaceError(bool validated)
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Fixture());
        var options = validated ? CanOpenWriteOptions.Validated : CanOpenWriteOptions.Default;

        // Act
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 5, timestamp: Timestamp);
        var act = () => CanOpenFile.Dcf.WriteToString(dcf, options);

        // Assert
        dcf.FileInfo.Description.Should().Be("DCF generated from EDS");
        act.Should().NotThrow();
        CanOpenFile.Dcf.ReadString(act()).FileInfo.Description.Should().Be("DCF generated from EDS");
    }

    [Fact]
    public void ConvertToDcf_FileName_DescriptionNamesSourceFile()
    {
        // Arrange
        var eds = CanOpenFile.Eds.ReadString(Fixture("FileName=device.eds"));

        // Act
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 5, timestamp: Timestamp);
        var written = CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        dcf.FileInfo.Description.Should().Be("DCF generated from device.eds");
        written.Should().Contain("Description=DCF generated from device.eds");
    }

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
}
