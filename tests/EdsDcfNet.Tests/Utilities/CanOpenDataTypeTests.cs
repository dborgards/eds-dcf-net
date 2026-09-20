namespace EdsDcfNet.Tests.Utilities;

using EdsDcfNet;
using EdsDcfNet.Utilities;
using AwesomeAssertions;
using Xunit;

public class CanOpenDataTypeTests
{
    // Full CiA 301 §7.4.7 table: code, name, fixed bit length (null = variable).
    public static IEnumerable<object[]> StandardTypes()
    {
        yield return new object[] { (ushort)0x0001, "BOOLEAN", 1 };
        yield return new object[] { (ushort)0x0002, "INTEGER8", 8 };
        yield return new object[] { (ushort)0x0003, "INTEGER16", 16 };
        yield return new object[] { (ushort)0x0004, "INTEGER32", 32 };
        yield return new object[] { (ushort)0x0005, "UNSIGNED8", 8 };
        yield return new object[] { (ushort)0x0006, "UNSIGNED16", 16 };
        yield return new object[] { (ushort)0x0007, "UNSIGNED32", 32 };
        yield return new object[] { (ushort)0x0008, "REAL32", 32 };
        yield return new object[] { (ushort)0x0009, "VISIBLE_STRING", null! };
        yield return new object[] { (ushort)0x000A, "OCTET_STRING", null! };
        yield return new object[] { (ushort)0x000B, "UNICODE_STRING", null! };
        yield return new object[] { (ushort)0x000C, "TIME_OF_DAY", 48 };
        yield return new object[] { (ushort)0x000D, "TIME_DIFFERENCE", 48 };
        yield return new object[] { (ushort)0x000F, "DOMAIN", null! };
        yield return new object[] { (ushort)0x0010, "INTEGER24", 24 };
        yield return new object[] { (ushort)0x0011, "REAL64", 64 };
        yield return new object[] { (ushort)0x0012, "INTEGER40", 40 };
        yield return new object[] { (ushort)0x0013, "INTEGER48", 48 };
        yield return new object[] { (ushort)0x0014, "INTEGER56", 56 };
        yield return new object[] { (ushort)0x0015, "INTEGER64", 64 };
        yield return new object[] { (ushort)0x0016, "UNSIGNED24", 24 };
        yield return new object[] { (ushort)0x0018, "UNSIGNED40", 40 };
        yield return new object[] { (ushort)0x0019, "UNSIGNED48", 48 };
        yield return new object[] { (ushort)0x001A, "UNSIGNED56", 56 };
        yield return new object[] { (ushort)0x001B, "UNSIGNED64", 64 };
    }

    [Theory]
    [MemberData(nameof(StandardTypes))]
    public void StandardType_HasExpectedMetadata(ushort code, string name, int? bits)
    {
        CanOpenDataType.IsStandardType(code).Should().BeTrue();
        CanOpenDataType.GetName(code).Should().Be(name);
        CanOpenDataType.TryGetBitLength(code).Should().Be(bits);
    }

    [Theory]
    // Reserved codes inside the standard range.
    [InlineData(0x0000)]
    [InlineData(0x000E)]
    [InlineData(0x0017)]
    [InlineData(0x001C)]
    // Manufacturer-specific range and beyond.
    [InlineData(0x0040)]
    [InlineData(0x00FF)]
    [InlineData(0xFFFF)]
    public void NonStandardType_HasNoMetadata(ushort code)
    {
        CanOpenDataType.IsStandardType(code).Should().BeFalse();
        CanOpenDataType.GetName(code).Should().BeNull();
        CanOpenDataType.TryGetBitLength(code).Should().BeNull();
        CanOpenDataType.IsSigned(code).Should().BeFalse();
        CanOpenDataType.IsUnsigned(code).Should().BeFalse();
    }

    [Theory]
    [InlineData(0x0002)]
    [InlineData(0x0003)]
    [InlineData(0x0004)]
    [InlineData(0x0010)]
    [InlineData(0x0012)]
    [InlineData(0x0013)]
    [InlineData(0x0014)]
    [InlineData(0x0015)]
    public void SignedIntegerTypes_AreSigned(ushort code)
    {
        CanOpenDataType.IsSigned(code).Should().BeTrue();
        CanOpenDataType.IsUnsigned(code).Should().BeFalse();
    }

    [Theory]
    [InlineData(0x0005)]
    [InlineData(0x0006)]
    [InlineData(0x0007)]
    [InlineData(0x0016)]
    [InlineData(0x0018)]
    [InlineData(0x0019)]
    [InlineData(0x001A)]
    [InlineData(0x001B)]
    public void UnsignedIntegerTypes_AreUnsigned(ushort code)
    {
        CanOpenDataType.IsUnsigned(code).Should().BeTrue();
        CanOpenDataType.IsSigned(code).Should().BeFalse();
    }

    /// <summary>
    /// The converter must derive its widths from the metadata table: for every fixed-width
    /// integer type, the all-ones literal at the table width round-trips, and one bit beyond
    /// the table width overflows. A drift between the table and the converter breaks this.
    /// </summary>
    public static IEnumerable<object[]> FixedWidthIntegerTypes()
    {
        ushort[] codes =
        {
            0x0002, 0x0003, 0x0004, 0x0010, 0x0012, 0x0013, 0x0014, 0x0015,
            0x0005, 0x0006, 0x0007, 0x0016, 0x0018, 0x0019, 0x001A, 0x001B,
        };
        foreach (var code in codes)
            yield return new object[] { code };
    }

    [Theory]
    [MemberData(nameof(FixedWidthIntegerTypes))]
    public void ConverterWidth_MatchesMetadataTable(ushort code)
    {
        var bits = CanOpenDataType.TryGetBitLength(code);
        bits.Should().NotBeNull();

        // All-ones at the table width parses and formats back at that width.
        var maxLiteral = "0x" + new string('F', bits!.Value / 4);
        var parsed = CanOpenValueConverter.Parse(maxLiteral, code);
        var formatted = CanOpenValueConverter.Format(parsed, code);

        // Re-parsing the formatted value must agree with the original parse.
        CanOpenValueConverter.Format(CanOpenValueConverter.Parse(formatted, code), code)
            .Should().Be(formatted);

        // One beyond the unsigned width overflows in the converter.
        var beyond = (1UL << Math.Min(bits.Value, 63)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (bits.Value < 64)
        {
            FluentActions.Invoking(() => CanOpenValueConverter.Parse(beyond, code))
                .Should().Throw<OverflowException>(
                    "a value one bit beyond the table width must overflow the converter");
        }
    }

    [Fact]
    public void ObjectTypeConstants_MatchCia306()
    {
        CanOpenObjectType.Null.Should().Be(0x0);
        CanOpenObjectType.Domain.Should().Be(0x2);
        CanOpenObjectType.DefType.Should().Be(0x5);
        CanOpenObjectType.DefStruct.Should().Be(0x6);
        CanOpenObjectType.Var.Should().Be(0x7);
        CanOpenObjectType.Array.Should().Be(0x8);
        CanOpenObjectType.Record.Should().Be(0x9);
    }
}
