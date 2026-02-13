namespace EdsDcfNet.Tests.Utilities;

using EdsDcfNet.Models;
using EdsDcfNet.Utilities;
using FluentAssertions;
using Xunit;

public class ValueConverterTests
{
    #region ParseInteger Tests

    [Theory]
    [InlineData("0", 0u)]
    [InlineData("1", 1u)]
    [InlineData("123", 123u)]
    [InlineData("65535", 65535u)]
    [InlineData("4294967295", 4294967295u)]
    public void ParseInteger_DecimalValues_ParsesCorrectly(string input, uint expected)
    {
        // Act
        var result = ValueConverter.ParseInteger(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("0x0", 0u)]
    [InlineData("0x1", 1u)]
    [InlineData("0xFF", 255u)]
    [InlineData("0x100", 256u)]
    [InlineData("0x1000", 4096u)]
    [InlineData("0xFFFF", 65535u)]
    [InlineData("0x00000191", 401u)]
    [InlineData("0X1A", 26u)] // Uppercase X
    public void ParseInteger_HexadecimalValues_ParsesCorrectly(string input, uint expected)
    {
        // Act
        var result = ValueConverter.ParseInteger(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("00", 0u)]
    [InlineData("01", 1u)]
    [InlineData("010", 8u)]
    [InlineData("0377", 255u)]
    [InlineData("01000", 512u)]
    public void ParseInteger_OctalValues_ParsesCorrectly(string input, uint expected)
    {
        // Act
        var result = ValueConverter.ParseInteger(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("  123  ", 123u)]
    [InlineData("  0xFF  ", 255u)]
    [InlineData("  010  ", 8u)]
    public void ParseInteger_WithWhitespace_TrimsAndParses(string input, uint expected)
    {
        // Act
        var result = ValueConverter.ParseInteger(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("", 0u)]
    [InlineData("   ", 0u)]
    public void ParseInteger_EmptyOrWhitespace_ReturnsZero(string input, uint expected)
    {
        // Act
        var result = ValueConverter.ParseInteger(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("$NODEID", 5, 5u)]
    [InlineData("$nodeid", 10, 10u)] // Case insensitive
    [InlineData("$NODEID+0x200", 5, 517u)] // 5 + 512
    [InlineData("$NODEID+0x180", 5, 389u)] // 5 + 384
    [InlineData("$NODEID+512", 10, 522u)] // 10 + 512
    [InlineData("$nodeid+0x200", 5, 517u)] // 5 + 512, lowercase with operator
    public void ParseInteger_NodeIdFormula_EvaluatesCorrectly(string formula, byte nodeId, uint expected)
    {
        // Act
        var result = ValueConverter.ParseInteger(formula, nodeId);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ParseInteger_NodeIdFormulaWithoutNodeId_ThrowsNotSupportedException()
    {
        // Arrange
        var formula = "$NODEID+0x200";

        // Act
        var act = () => ValueConverter.ParseInteger(formula);

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Cannot evaluate $NODEID formula*");
    }

    #endregion

    #region ParseBoolean Tests

    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("yes", true)]
    [InlineData("Yes", true)]
    [InlineData("YES", true)]
    public void ParseBoolean_TrueValues_ReturnsTrue(string input, bool expected)
    {
        // Act
        var result = ValueConverter.ParseBoolean(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("no", false)]
    [InlineData("No", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("2", false)]
    [InlineData("random", false)]
    public void ParseBoolean_FalseValues_ReturnsFalse(string input, bool expected)
    {
        // Act
        var result = ValueConverter.ParseBoolean(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("  1  ", true)]
    [InlineData("  true  ", true)]
    [InlineData("  0  ", false)]
    public void ParseBoolean_WithWhitespace_TrimsAndParses(string input, bool expected)
    {
        // Act
        var result = ValueConverter.ParseBoolean(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region ParseByte Tests

    [Theory]
    [InlineData("0", (byte)0)]
    [InlineData("1", (byte)1)]
    [InlineData("255", (byte)255)]
    public void ParseByte_DecimalValues_ParsesCorrectly(string input, byte expected)
    {
        // Act
        var result = ValueConverter.ParseByte(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("0x0", (byte)0)]
    [InlineData("0xFF", (byte)255)]
    [InlineData("0x10", (byte)16)]
    public void ParseByte_HexadecimalValues_ParsesCorrectly(string input, byte expected)
    {
        // Act
        var result = ValueConverter.ParseByte(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("00", (byte)0)]
    [InlineData("010", (byte)8)]
    [InlineData("0377", (byte)255)]
    public void ParseByte_OctalValues_ParsesCorrectly(string input, byte expected)
    {
        // Act
        var result = ValueConverter.ParseByte(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("", (byte)0)]
    [InlineData("   ", (byte)0)]
    public void ParseByte_EmptyOrWhitespace_ReturnsZero(string input, byte expected)
    {
        // Act
        var result = ValueConverter.ParseByte(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region ParseUInt16 Tests

    [Theory]
    [InlineData("0", (ushort)0)]
    [InlineData("1", (ushort)1)]
    [InlineData("65535", (ushort)65535)]
    public void ParseUInt16_DecimalValues_ParsesCorrectly(string input, ushort expected)
    {
        // Act
        var result = ValueConverter.ParseUInt16(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("0x0", (ushort)0)]
    [InlineData("0xFFFF", (ushort)65535)]
    [InlineData("0x1000", (ushort)4096)]
    public void ParseUInt16_HexadecimalValues_ParsesCorrectly(string input, ushort expected)
    {
        // Act
        var result = ValueConverter.ParseUInt16(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("00", (ushort)0)]
    [InlineData("010", (ushort)8)]
    [InlineData("0177777", (ushort)65535)]
    public void ParseUInt16_OctalValues_ParsesCorrectly(string input, ushort expected)
    {
        // Act
        var result = ValueConverter.ParseUInt16(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("", (ushort)0)]
    [InlineData("   ", (ushort)0)]
    public void ParseUInt16_EmptyOrWhitespace_ReturnsZero(string input, ushort expected)
    {
        // Act
        var result = ValueConverter.ParseUInt16(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region ParseAccessType Tests

    [Theory]
    [InlineData("ro", AccessType.ReadOnly)]
    [InlineData("RO", AccessType.ReadOnly)]
    [InlineData("Ro", AccessType.ReadOnly)]
    [InlineData("  ro  ", AccessType.ReadOnly)]
    public void ParseAccessType_ReadOnly_ReturnsReadOnly(string input, AccessType expected)
    {
        // Act
        var result = ValueConverter.ParseAccessType(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("wo", AccessType.WriteOnly)]
    [InlineData("WO", AccessType.WriteOnly)]
    public void ParseAccessType_WriteOnly_ReturnsWriteOnly(string input, AccessType expected)
    {
        // Act
        var result = ValueConverter.ParseAccessType(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("rw", AccessType.ReadWrite)]
    [InlineData("RW", AccessType.ReadWrite)]
    public void ParseAccessType_ReadWrite_ReturnsReadWrite(string input, AccessType expected)
    {
        // Act
        var result = ValueConverter.ParseAccessType(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("rwr", AccessType.ReadWriteInput)]
    [InlineData("RWR", AccessType.ReadWriteInput)]
    public void ParseAccessType_ReadWriteInput_ReturnsReadWriteInput(string input, AccessType expected)
    {
        // Act
        var result = ValueConverter.ParseAccessType(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("rww", AccessType.ReadWriteOutput)]
    [InlineData("RWW", AccessType.ReadWriteOutput)]
    public void ParseAccessType_ReadWriteOutput_ReturnsReadWriteOutput(string input, AccessType expected)
    {
        // Act
        var result = ValueConverter.ParseAccessType(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("const", AccessType.Constant)]
    [InlineData("CONST", AccessType.Constant)]
    [InlineData("Const", AccessType.Constant)]
    public void ParseAccessType_Constant_ReturnsConstant(string input, AccessType expected)
    {
        // Act
        var result = ValueConverter.ParseAccessType(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseAccessType_InvalidOrEmpty_ReturnsReadOnly(string? input)
    {
        // Act
        var result = ValueConverter.ParseAccessType(input!);

        // Assert
        result.Should().Be(AccessType.ReadOnly);
    }

    #endregion

    #region AccessTypeToString Tests

    [Fact]
    public void AccessTypeToString_ReadOnly_ReturnsRo()
    {
        // Act
        var result = ValueConverter.AccessTypeToString(AccessType.ReadOnly);

        // Assert
        result.Should().Be("ro");
    }

    [Fact]
    public void AccessTypeToString_WriteOnly_ReturnsWo()
    {
        // Act
        var result = ValueConverter.AccessTypeToString(AccessType.WriteOnly);

        // Assert
        result.Should().Be("wo");
    }

    [Fact]
    public void AccessTypeToString_ReadWrite_ReturnsRw()
    {
        // Act
        var result = ValueConverter.AccessTypeToString(AccessType.ReadWrite);

        // Assert
        result.Should().Be("rw");
    }

    [Fact]
    public void AccessTypeToString_ReadWriteInput_ReturnsRwr()
    {
        // Act
        var result = ValueConverter.AccessTypeToString(AccessType.ReadWriteInput);

        // Assert
        result.Should().Be("rwr");
    }

    [Fact]
    public void AccessTypeToString_ReadWriteOutput_ReturnsRww()
    {
        // Act
        var result = ValueConverter.AccessTypeToString(AccessType.ReadWriteOutput);

        // Assert
        result.Should().Be("rww");
    }

    [Fact]
    public void AccessTypeToString_Constant_ReturnsConst()
    {
        // Act
        var result = ValueConverter.AccessTypeToString(AccessType.Constant);

        // Assert
        result.Should().Be("const");
    }

    #endregion

    #region FormatInteger Tests

    [Theory]
    [InlineData(0u, true, "0x0")]
    [InlineData(1u, true, "0x1")]
    [InlineData(255u, true, "0xFF")]
    [InlineData(256u, true, "0x100")]
    [InlineData(4096u, true, "0x1000")]
    [InlineData(65535u, true, "0xFFFF")]
    public void FormatInteger_WithHex_FormatsCorrectly(uint value, bool useHex, string expected)
    {
        // Act
        var result = ValueConverter.FormatInteger(value, useHex);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0u, false, "0")]
    [InlineData(1u, false, "1")]
    [InlineData(255u, false, "255")]
    [InlineData(65535u, false, "65535")]
    public void FormatInteger_WithoutHex_FormatsDecimal(uint value, bool useHex, string expected)
    {
        // Act
        var result = ValueConverter.FormatInteger(value, useHex);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0u, "0x0")]
    [InlineData(255u, "0xFF")]
    public void FormatInteger_DefaultParameter_UsesHex(uint value, string expected)
    {
        // Act
        var result = ValueConverter.FormatInteger(value);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region FormatBoolean Tests

    [Fact]
    public void FormatBoolean_True_ReturnsOne()
    {
        // Act
        var result = ValueConverter.FormatBoolean(true);

        // Assert
        result.Should().Be("1");
    }

    [Fact]
    public void FormatBoolean_False_ReturnsZero()
    {
        // Act
        var result = ValueConverter.FormatBoolean(false);

        // Assert
        result.Should().Be("0");
    }

    #endregion

    #region Round-trip Tests

    [Theory]
    [InlineData("ro")]
    [InlineData("wo")]
    [InlineData("rw")]
    [InlineData("rwr")]
    [InlineData("rww")]
    [InlineData("const")]
    public void AccessType_RoundTrip_PreservesValue(string input)
    {
        // Act
        var accessType = ValueConverter.ParseAccessType(input);
        var output = ValueConverter.AccessTypeToString(accessType);

        // Assert
        output.Should().Be(input);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Boolean_RoundTrip_PreservesValue(bool input)
    {
        // Act
        var formatted = ValueConverter.FormatBoolean(input);
        var parsed = ValueConverter.ParseBoolean(formatted);

        // Assert
        parsed.Should().Be(input);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(255u)]
    [InlineData(65535u)]
    [InlineData(4294967295u)]
    public void Integer_HexRoundTrip_PreservesValue(uint input)
    {
        // Act
        var formatted = ValueConverter.FormatInteger(input, useHex: true);
        var parsed = ValueConverter.ParseInteger(formatted);

        // Assert
        parsed.Should().Be(input);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(255u)]
    [InlineData(65535u)]
    public void Integer_DecimalRoundTrip_PreservesValue(uint input)
    {
        // Act
        var formatted = ValueConverter.FormatInteger(input, useHex: false);
        var parsed = ValueConverter.ParseInteger(formatted);

        // Assert
        parsed.Should().Be(input);
    }

    #endregion
}
