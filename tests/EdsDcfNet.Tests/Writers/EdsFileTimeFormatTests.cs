namespace EdsDcfNet.Tests.Writers;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Utilities;
using EdsDcfNet.Validation;
using EdsDcfNet.Writers;

/// <summary>
/// CiA 306-1 v1.4.0 § 6.4 "File information", Table 1 (<c>[FileInfo]</c>): <c>CreationTime</c> is
/// "the file creation time as 'hh:mm(AM|PM)'" and <c>ModificationTime</c> "the time of last
/// modification as 'hh:mm(AM|PM)'". A time read from an XDD/XDC as <c>xsd:time</c> is converted
/// (time zone not converted, seconds dropped); a value that is neither is written unchanged and
/// reported by a validated write.
/// </summary>
public class EdsFileTimeFormatTests
{
    private static readonly string BasicDevicePath =
        Path.Combine("Fixtures", "Corpus", "canopen-node", "basicDevice.xdd");

    // ── XDD → EDS / DCF (acceptance) ────────────────────────────────────────

    [Fact]
    public void WriteToString_BasicDeviceXddWrittenAsEds_WritesTimesInCiA306Format()
    {
        // Arrange
        var xdd = CanOpenFile.Xdd.ReadFile(BasicDevicePath);

        // Act
        var text = CanOpenFile.Eds.WriteToString(xdd);

        // Assert
        text.Should().Contain("CreationTime=12:37PM");
        text.Should().Contain("ModificationTime=07:31PM");
        CanOpenFile.Eds.ReadString(text).FileInfo.CreationTime.Should().Be("12:37PM");
    }

    [Fact]
    public void WriteToString_BasicDeviceXddWrittenAsEds_ValidatedRoundTripKeepsTimes()
    {
        // Arrange
        var xdd = CanOpenFile.Xdd.ReadFile(BasicDevicePath);

        // Act
        var first = CanOpenFile.Eds.WriteToString(xdd);
        var again = CanOpenFile.Eds.WriteToString(CanOpenFile.Eds.ReadString(first), CanOpenWriteOptions.Validated);

        // Assert
        again.Should().Contain("CreationTime=12:37PM").And.Contain("ModificationTime=07:31PM");
    }

    [Fact]
    public void WriteToString_XsdTimesInDcfModel_WritesTimesInCiA306Format()
    {
        // Arrange
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.FileInfo.CreationTime = "19:31:59.7179280+01:00";
        dcf.FileInfo.ModificationTime = "00:00:00Z";

        // Act
        var text = CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        text.Should().Contain("CreationTime=07:31PM").And.Contain("ModificationTime=12:00AM");
    }

    [Fact]
    public void WriteToString_EdsToEds_KeepsTimesUnchanged()
    {
        // Arrange
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.FileInfo.CreationTime = "09:05AM";
        eds.FileInfo.ModificationTime = "11:59PM";

        // Act
        var text = CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        text.Should().Contain("CreationTime=09:05AM").And.Contain("ModificationTime=11:59PM");
    }

    [Fact]
    public void WriteToString_ValidXsdTimeWrittenAsXdd_ValidatedKeepsValueUntouched()
    {
        // Arrange
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.FileInfo.CreationTime = "19:31:59.7179280+01:00";

        // Act
        var text = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        text.Should().Contain("fileCreationTime=\"19:31:59.7179280+01:00\"");
    }

    // ── Conversion boundaries ───────────────────────────────────────────────

    [Theory]
    [InlineData("00:00:00", "12:00AM")]
    [InlineData("00:59:59", "12:59AM")]
    [InlineData("01:00:00", "01:00AM")]
    [InlineData("11:59:59", "11:59AM")]
    [InlineData("12:00:00", "12:00PM")]
    [InlineData("12:37:54.0000000+01:00", "12:37PM")]
    [InlineData("13:00:00", "01:00PM")]
    [InlineData("19:31:59.7179280+01:00", "07:31PM")]
    [InlineData("23:59:59", "11:59PM")]
    [InlineData("23:59:59.999Z", "11:59PM")]
    [InlineData("10:15:00-05:00", "10:15AM")]
    [InlineData("24:00:00", "12:00AM")]
    [InlineData("07:31PM", "07:31PM")]
    [InlineData("7:31pm", "7:31pm")]
    public void TryConvertFileTimeToEds_ReadableTime_ReturnsCiA306Form(string input, string expected)
    {
        // Act
        var ok = XddFormatHelper.TryConvertFileTimeToEds(input, out var result);

        // Assert
        ok.Should().BeTrue();
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("noon")]
    [InlineData("25:00:00")]
    [InlineData("12:60:00")]
    [InlineData("13:00PM")]
    [InlineData("12:37")]
    public void TryConvertFileTimeToEds_UnreadableTime_ReturnsFalse(string? input)
    {
        // Act
        var ok = XddFormatHelper.TryConvertFileTimeToEds(input, out var result);

        // Assert
        ok.Should().BeFalse();
        result.Should().BeEmpty();
    }

    // ── Validation modes (Rule 14) ──────────────────────────────────────────

    [Fact]
    public void WriteToString_UnreadableTime_ValidatedEdsRejectsAndPlainWritesUnchanged()
    {
        // Arrange
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.FileInfo.CreationTime = "noon";
        eds.FileInfo.ModificationTime = "25:00:00";

        // Act
        var validated = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);
        var plain = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        var issues = validated.Should().Throw<ModelValidationException>().Which.Issues;
        issues.Should().Contain(i => i.Path == "FileInfo.CreationTime" && i.Code == ValidationIssueCodes.IniFileTimeInvalid);
        issues.Should().Contain(i => i.Path == "FileInfo.ModificationTime" && i.Code == ValidationIssueCodes.IniFileTimeInvalid);
        plain.Should().Contain("CreationTime=noon").And.Contain("ModificationTime=25:00:00");
    }

    [Fact]
    public void WriteToString_UnreadableTime_ValidatedDcfRejects()
    {
        // Arrange
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.FileInfo.ModificationTime = "noon";

        // Act
        var act = () => CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        act.Should().Throw<ModelValidationException>().Which.Issues
            .Should().Contain(i => i.Path == "FileInfo.ModificationTime" && i.Code == ValidationIssueCodes.IniFileTimeInvalid);
    }

    [Fact]
    public void WriteToString_EmptyTime_ValidatedEdsDoesNotReportTime()
    {
        // Arrange
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.FileInfo.CreationTime = string.Empty;

        // Act
        var text = CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        text.Should().Contain("CreationTime=");
    }

    [Fact]
    public void WriteToString_UnreadableTime_ValidatedXddReportsOnlyXddCode()
    {
        // Arrange
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.FileInfo.ModificationTime = "noon";

        // Act
        var act = () => CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        var issues = act.Should().Throw<ModelValidationException>().Which.Issues;
        issues.Should().Contain(i => i.Code == ValidationIssueCodes.XddFileTimeInvalid);
        issues.Should().NotContain(i => i.Code == ValidationIssueCodes.IniFileTimeInvalid);
    }

    // ── Baud rates only XDD/XDC can carry ───────────────────────────────────

    [Theory]
    [InlineData(true, false, "DeviceInfo.SupportedBaudRates.BaudRate100")]
    [InlineData(false, true, "DeviceInfo.SupportedBaudRates.AutoBaudRate")]
    public void WriteToString_XddOnlyBaudRate_ValidatedEdsAndDcfRejectPlainOmits(bool rate100, bool auto, string path)
    {
        // Arrange
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.DeviceInfo.SupportedBaudRates.BaudRate100 = rate100;
        eds.DeviceInfo.SupportedBaudRates.AutoBaudRate = auto;
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.DeviceInfo.SupportedBaudRates.BaudRate100 = rate100;
        dcf.DeviceInfo.SupportedBaudRates.AutoBaudRate = auto;

        // Act
        var validatedEds = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);
        var validatedDcf = () => CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);
        var plain = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        validatedEds.Should().Throw<ModelValidationException>().Which.Issues
            .Should().Contain(i => i.Path == path && i.Code == ValidationIssueCodes.IniBaudRateNotRepresentable);
        validatedDcf.Should().Throw<ModelValidationException>().Which.Issues
            .Should().Contain(i => i.Path == path && i.Code == ValidationIssueCodes.IniBaudRateNotRepresentable);
        plain.Should().NotContain("BaudRate_100=").And.NotContain("auto-baudRate");
    }

    [Fact]
    public void WriteToString_XddOnlyBaudRates_ValidatedXddIsNotAffected()
    {
        // Arrange
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.DeviceInfo.SupportedBaudRates.BaudRate100 = true;
        eds.DeviceInfo.SupportedBaudRates.AutoBaudRate = true;

        // Act
        var text = CanOpenFile.Xdd.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        text.Should().Contain("100 Kbps").And.Contain("auto-baudRate");
    }
}
