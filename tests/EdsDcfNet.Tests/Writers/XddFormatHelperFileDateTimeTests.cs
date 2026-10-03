namespace EdsDcfNet.Tests.Writers;

using EdsDcfNet.Writers;

/// <summary>
/// Lexical rules for <c>xsd:date</c> and <c>xsd:time</c> (XML Schema Part 2) and the EDS
/// <c>MM-DD-YYYY</c> / <c>hh:mmAM/PM</c> forms used for the CiA 311 file attributes.
/// </summary>
public class XddFormatHelperFileDateTimeTests
{
    [Theory]
    [InlineData("2026-01-01", true)]
    [InlineData("2024-02-29", true)]
    [InlineData("2026-02-29", false)]
    [InlineData("1900-02-29", false)]
    [InlineData("2000-02-29", true)]
    [InlineData("2026-04-31", false)]
    [InlineData("2026-12-31", true)]
    [InlineData("2026-13-01", false)]
    [InlineData("2026-00-10", false)]
    [InlineData("2026-01-00", false)]
    [InlineData("2026-01-32", false)]
    [InlineData("0000-01-01", false)]
    [InlineData("0001-01-01", true)]
    [InlineData("-0001-01-01", true)]
    [InlineData("12345-01-01", true)]
    [InlineData("10000-02-29", true)]
    [InlineData("12100-02-29", false)]
    [InlineData("012345-01-01", false)]
    [InlineData("026-01-01", false)]
    [InlineData("2026-1-01", false)]
    [InlineData("2026-01-01Z", true)]
    [InlineData("2026-01-01+05:30", true)]
    [InlineData("2026-01-01-14:00", true)]
    [InlineData("2026-01-01+14:00", true)]
    [InlineData("2026-01-01+14:01", false)]
    [InlineData("2026-01-01+15:00", false)]
    [InlineData("2026-01-01+05:60", false)]
    [InlineData("2026-01-01+0530", false)]
    [InlineData("2026-01-01z", false)]
    [InlineData("2026-01-01\n", false)]
    [InlineData("2026-01-01 ", false)]
    [InlineData("٢٠٢٦-01-01", false)]
    [InlineData("banana", false)]
    [InlineData("", false)]
    public void IsXsdDate_Text_FollowsXmlSchemaLexicalRules(string text, bool expected)
    {
        // Act
        var result = XddFormatHelper.IsXsdDate(text);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("00:00:00", true)]
    [InlineData("23:59:59", true)]
    [InlineData("24:00:00", true)]
    [InlineData("24:00:00.000", true)]
    [InlineData("24:00:00.5", false)]
    [InlineData("24:00:01", false)]
    [InlineData("24:01:00", false)]
    [InlineData("25:00:00", false)]
    [InlineData("10:60:00", false)]
    [InlineData("10:00:60", false)]
    [InlineData("10:00:00.7179280", true)]
    [InlineData("10:00:00.", false)]
    [InlineData("10:00:00Z", true)]
    [InlineData("10:00:00+01:00", true)]
    [InlineData("10:00:00-14:00", true)]
    [InlineData("10:00:00+14:30", false)]
    [InlineData("19:31:59.7179280+01:00", true)]
    [InlineData("10:00", false)]
    [InlineData("1:00:00", false)]
    [InlineData("10:00:00\n", false)]
    [InlineData("10:00AM", false)]
    [InlineData("", false)]
    public void IsXsdTime_Text_FollowsXmlSchemaLexicalRules(string text, bool expected)
    {
        // Act
        var result = XddFormatHelper.IsXsdTime(text);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("05-24-2024", true, "2024-05-24")]
    [InlineData(" 05-24-2024 ", true, "2024-05-24")]
    [InlineData("02-29-2024", true, "2024-02-29")]
    [InlineData("02-29-2000", true, "2000-02-29")]
    [InlineData("02-29-1900", false, "")]
    [InlineData("02-30-2026", false, "")]
    [InlineData("04-31-2026", false, "")]
    [InlineData("13-01-2026", false, "")]
    [InlineData("00-01-2026", false, "")]
    [InlineData("01-00-2026", false, "")]
    [InlineData("01-32-2026", false, "")]
    [InlineData("01-01-0000", false, "")]
    [InlineData("01-01-0001", true, "0001-01-01")]
    [InlineData("2026-01-01+05:30", true, "2026-01-01+05:30")]
    [InlineData("2026-02-30", false, "")]
    [InlineData("banana", false, "")]
    [InlineData("", false, "")]
    public void TryConvertEdsDateToXsd_Text_ConvertsOnlyExistingDates(string text, bool expected, string xsd)
    {
        // Act
        var result = XddFormatHelper.TryConvertEdsDateToXsd(text, out var converted);

        // Assert
        result.Should().Be(expected);
        converted.Should().Be(xsd);
    }

    [Fact]
    public void TryConvertEdsDateToXsd_Null_ReturnsFalse()
    {
        // Act
        var result = XddFormatHelper.TryConvertEdsDateToXsd(null, out var converted);

        // Assert
        result.Should().BeFalse();
        converted.Should().BeEmpty();
    }

    [Theory]
    [InlineData("2026-01-01", "01-01-2026")]
    [InlineData("  2026-01-01+05:30 ", "01-01-2026")]
    [InlineData("2026-30-02", "30-02-2026")]
    [InlineData("12345-01-01", "12345-01-01")]
    [InlineData("banana", "banana")]
    [InlineData("  ", "")]
    [InlineData(null, "")]
    public void ConvertXsdDateToEds_Text_ConvertsLeadingDatePartOnly(string? text, string expected)
    {
        // Act
        var result = XddFormatHelper.ConvertXsdDateToEds(text);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("01-01-2026", "2026-01-01+05:30", true, "2026-01-01+05:30")]
    [InlineData(" 01-01-2026 ", "2026-01-01+05:30", true, "2026-01-01+05:30")]
    [InlineData("02-03-2026", "2026-01-01+05:30", true, "2026-02-03")]
    [InlineData("01-01-2026", null, true, "2026-01-01")]
    [InlineData("banana", "banana", false, "")]
    [InlineData(null, "2026-01-01Z", false, "")]
    public void TryFormatFileDate_PreservedSpelling_IsUsedOnlyWhileTheModelDateMatches(
        string? modelDate, string? preserved, bool expected, string xsd)
    {
        // Act
        var result = XddFormatHelper.TryFormatFileDate(modelDate, preserved, out var formatted);

        // Assert
        result.Should().Be(expected);
        formatted.Should().Be(xsd);
    }

    [Theory]
    [InlineData("02:30PM", "14:30:00")]
    [InlineData("2:07AM", "02:07:00")]
    [InlineData("12:00AM", "00:00:00")]
    [InlineData("12:34AM", "00:34:00")]
    [InlineData("12:00PM", "12:00:00")]
    [InlineData("12:59pm", "12:59:00")]
    [InlineData("01:00PM", "13:00:00")]
    [InlineData("11:59PM", "23:59:00")]
    [InlineData("10:00:00", "10:00:00")]
    [InlineData(" 10:00:00Z ", "10:00:00Z")]
    public void TryConvertFileTimeToXsd_ValidTime_ReturnsXsdTime(string text, string expected)
    {
        // Act
        var result = XddFormatHelper.TryConvertFileTimeToXsd(text, out var converted);

        // Assert
        result.Should().BeTrue();
        converted.Should().Be(expected);
    }

    [Theory]
    [InlineData("00:30AM")]
    [InlineData("13:00PM")]
    [InlineData("12:60PM")]
    [InlineData("10:00XM")]
    [InlineData("10:00 AM")]
    [InlineData("100:00AM")]
    [InlineData("banana")]
    [InlineData("")]
    [InlineData(null)]
    public void TryConvertFileTimeToXsd_InvalidTime_ReturnsFalse(string? text)
    {
        // Act
        var result = XddFormatHelper.TryConvertFileTimeToXsd(text, out var converted);

        // Assert
        result.Should().BeFalse();
        converted.Should().BeEmpty();
    }
}
