namespace EdsDcfNet.Tests.Utilities;

using EdsDcfNet.Extensions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// CiA 306-1 v1.4.0 section 6.3: "The octet strings and raw data of domains shall be stored as a
/// sequence of hexadecimal bytes without leading "0x". Bytes with a high nibble of "0" shall be
/// stored with the leading "0"." (example: DemoSeq=01a1053c45aabbccddeeff).
/// </summary>
public class OctetStringFormatTests
{
    private const ushort OctetString = 0x000A;

    [Fact]
    public void Format_OctetString_HasNoPrefixAndUsesUppercaseHex()
    {
        CanOpenValueConverter.Format(new byte[] { 0x01, 0xA1, 0x05, 0x3C, 0x45, 0xAA }, OctetString)
            .Should().Be("01A1053C45AA");
    }

    [Fact]
    public void Format_EmptyOctetString_ReturnsExplicitEmptyMarker()
    {
        // An empty string would be indistinguishable from "not set"; CiA 306-1 has no other
        // textual form for zero bytes, so the bare "0x" marker (accepted by Parse) is kept.
        CanOpenValueConverter.Format(Array.Empty<byte>(), OctetString).Should().Be("0x");
        ((byte[])CanOpenValueConverter.Parse("0x", OctetString)).Should().BeEmpty();
    }

    [Fact]
    public void SetParameterValue_EmptyByteArray_OverridesNonEmptyDefaultAndSurvivesDcfRoundTrip()
    {
        foreach (var options in new[] { CanOpenWriteOptions.Default, CanOpenWriteOptions.Validated })
        {
            var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
            dcf.ObjectDictionary.Objects[0x2000] = CreateObject("0102");
            dcf.ObjectDictionary.OptionalObjects.Add(0x2000);
            dcf.ObjectDictionary.SetParameterValue(0x2000, Array.Empty<byte>()).Should().BeTrue();

            dcf.ObjectDictionary.GetParameterValue<byte[]>(0x2000).Should().BeEmpty();

            var text = CanOpenFile.Dcf.WriteToString(dcf, options);
            var reread = CanOpenFile.Dcf.ReadString(text);

            text.Should().Contain("ParameterValue=0x");
            reread.ObjectDictionary.GetParameterValue<byte[]>(0x2000).Should().BeEmpty();
        }
    }

    [Fact]
    public void Format_SingleByte_KeepsLeadingZeroNibble()
    {
        CanOpenValueConverter.Format(new byte[] { 0x0A }, OctetString).Should().Be("0A");
        CanOpenValueConverter.Format(new byte[] { 0x00 }, OctetString).Should().Be("00");
    }

    [Theory]
    [InlineData(new byte[] { 0x12, 0x34 }, "1234")]         // looks like a decimal number
    [InlineData(new byte[] { 0x01, 0x23, 0x45 }, "012345")] // looks like an octal number
    [InlineData(new byte[] { 0x00, 0x10 }, "0010")]
    [InlineData(new byte[] { 0x10, 0x00 }, "1000")]
    public void Format_DigitOnlyOctetString_IsNotTreatedAsNumber(byte[] bytes, string expected)
    {
        CanOpenValueConverter.Format(bytes, OctetString).Should().Be(expected);
        ((byte[])CanOpenValueConverter.Parse(expected, OctetString)).Should().Equal(bytes);
    }

    [Theory]
    [InlineData("01A1053C", new byte[] { 0x01, 0xA1, 0x05, 0x3C })]
    [InlineData("0x01A1053C", new byte[] { 0x01, 0xA1, 0x05, 0x3C })]
    [InlineData("0X01a1053c", new byte[] { 0x01, 0xA1, 0x05, 0x3C })]
    [InlineData("0102", new byte[] { 0x01, 0x02 })]
    [InlineData("", new byte[0])]
    public void Parse_OctetString_AcceptsBothForms(string text, byte[] expected)
    {
        ((byte[])CanOpenValueConverter.Parse(text, OctetString)).Should().Equal(expected);
    }

    [Fact]
    public void Format_Domain_RemainsUnsupported()
    {
        // DOMAIN raw data is also specified without 0x (CiA 306-1 6.3), but the converter rejects
        // DOMAIN; this change intentionally leaves that behaviour unchanged.
        var act = () => CanOpenValueConverter.Format(new byte[] { 0x01 }, 0x000F);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void SetParameterValue_ByteArray_StoresWithoutPrefix()
    {
        var dictionary = CreateDictionary();

        dictionary.SetParameterValue(0x2000, new byte[] { 0x01, 0xA1, 0x05 }).Should().BeTrue();
        dictionary.SetParameterValue(0x2000, 1, new byte[] { 0x0F }).Should().BeTrue();

        dictionary.Objects[0x2000].ParameterValue.Should().Be("01A105");
        dictionary.Objects[0x2000].SubObjects[1].ParameterValue.Should().Be("0F");
        dictionary.GetParameterValue<byte[]>(0x2000).Should().Equal(0x01, 0xA1, 0x05);
    }

    public static IEnumerable<object[]> RoundTripValues() => new[]
    {
        new object[] { new byte[] { 0x01, 0xA1, 0x05, 0x3C } },
        new object[] { new byte[] { 0x12, 0x34 } },
        new object[] { new byte[] { 0x01, 0x23, 0x45 } },
        new object[] { new byte[] { 0x00 } },
        new object[] { Array.Empty<byte>() },
    };

    [Theory]
    [MemberData(nameof(RoundTripValues))]
    public void EdsRoundTrip_OctetStringDefault_IsWrittenWithoutPrefix(byte[] bytes)
    {
        var expected = BitConverter.ToString(bytes).Replace("-", string.Empty); // independent of the converter

        foreach (var options in new[] { CanOpenWriteOptions.Default, CanOpenWriteOptions.Validated })
        {
            var eds = ValidCanOpenModelBuilder.CreateValidEds();
            eds.ObjectDictionary.Objects[0x2000] = CreateObject(expected);
            eds.ObjectDictionary.OptionalObjects.Add(0x2000);

            var text = CanOpenFile.Eds.WriteToString(eds, options);
            var reread = CanOpenFile.Eds.ReadString(text);

            text.Should().NotContain("DefaultValue=0x");
            if (bytes.Length == 0)
            {
                // The writer omits empty values; nothing to read back.
                continue;
            }

            text.Should().Contain("DefaultValue=" + expected);
            ((byte[])CanOpenValueConverter.Parse(reread.ObjectDictionary.Objects[0x2000].DefaultValue!, OctetString))
                .Should().Equal(bytes);
        }
    }

    [Theory]
    [MemberData(nameof(RoundTripValues))]
    public void DcfRoundTrip_OctetStringParameterValue_IsWrittenWithoutPrefix(byte[] bytes)
    {
        var expected = BitConverter.ToString(bytes).Replace("-", string.Empty); // independent of the converter

        foreach (var options in new[] { CanOpenWriteOptions.Default, CanOpenWriteOptions.Validated })
        {
            var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
            dcf.ObjectDictionary.Objects[0x2000] = CreateObject(null);
            dcf.ObjectDictionary.OptionalObjects.Add(0x2000);
            dcf.ObjectDictionary.SetParameterValue(0x2000, bytes).Should().BeTrue();

            var text = CanOpenFile.Dcf.WriteToString(dcf, options);
            var reread = CanOpenFile.Dcf.ReadString(text);

            var written = bytes.Length == 0 ? "0x" : expected; // empty keeps an explicit marker
            text.Should().Contain("ParameterValue=" + written);
            text.Should().Contain("ParameterValue=" + written + Environment.NewLine);
            if (bytes.Length > 0)
            {
                text.Should().NotContain("ParameterValue=0x");
            }

            reread.ObjectDictionary.GetParameterValue<byte[]>(0x2000).Should().Equal(bytes);
        }
    }

    private static CanOpenObject CreateObject(string? defaultValue) => new()
    {
        Index = 0x2000,
        ParameterName = "Demo Sequence",
        ObjectType = 0x7,
        DataType = OctetString,
        AccessType = AccessType.ReadWrite,
        DefaultValue = defaultValue
    };

    private static ObjectDictionary CreateDictionary()
    {
        var dictionary = new ObjectDictionary();
        var obj = CreateObject(null);
        obj.SubObjects[1] = new CanOpenSubObject { SubIndex = 1, DataType = OctetString };
        dictionary.Objects[0x2000] = obj;
        return dictionary;
    }
}
