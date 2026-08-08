namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Parsers;
using EdsDcfNet.Utilities;

public class SectionNumericParserTests
{
    [Theory]
    [InlineData("010", (byte)8)]
    [InlineData("0x0A", (byte)10)]
    [InlineData("10", (byte)10)]
    public void ParseUnsigned8_WithoutMajorMinor_UsesParseByteIncludingOctal(
        string value, byte expected)
    {
        // allowMajorMinorVersionForm: false keeps CiA octal via ParseByte.
        SectionNumericParser.ParseUnsigned8("FileInfo", "SomeKey", value, allowMajorMinorVersionForm: false)
            .Should().Be(expected);
    }

    [Fact]
    public void ParseUnsigned8_WithoutMajorMinor_Invalid_WrapsWithSectionKey()
    {
        var act = () => SectionNumericParser.ParseUnsigned8(
            "FileInfo", "SomeKey", "NaN", allowMajorMinorVersionForm: false);

        var ex = act.Should().Throw<EdsParseException>().Which;
        ex.SectionName.Should().Be("FileInfo");
        ex.Message.Should().Contain("[FileInfo] SomeKey:");
        ex.Message.Should().Contain("NaN");
    }

    [Theory]
    [InlineData("010", (byte)10)]
    [InlineData("08", (byte)8)]
    public void ParseUnsigned8_WithMajorMinor_PlainZeroPadded_UsesDecimal(string value, byte expected)
    {
        SectionNumericParser.ParseUnsigned8("FileInfo", "FileVersion", value, allowMajorMinorVersionForm: true)
            .Should().Be(expected);
    }

    [Fact]
    public void ParseUnsigned8_WithMajorMinor_LenientMajorMinor_UsesMajor()
    {
        SectionNumericParser.ParseUnsigned8("FileInfo", "FileVersion", "012.5", allowMajorMinorVersionForm: true)
            .Should().Be(12);
    }

    [Fact]
    public void ParseUnsigned8_WithMajorMinor_StrictMajorMinor_ThrowsWithAttribution()
    {
        using (StrictParsingScope.Enter(true))
        {
            var act = () => SectionNumericParser.ParseUnsigned8(
                "FileInfo", "FileVersion", "1.0", allowMajorMinorVersionForm: true);

            var ex = act.Should().Throw<EdsParseException>().Which;
            ex.SectionName.Should().Be("FileInfo");
            ex.Message.Should().Contain("[FileInfo] FileVersion:");
            ex.Message.Should().Contain("1.0");
        }
    }
}
