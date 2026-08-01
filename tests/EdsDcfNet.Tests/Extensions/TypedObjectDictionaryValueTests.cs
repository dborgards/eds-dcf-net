namespace EdsDcfNet.Tests.Extensions;

using EdsDcfNet.Extensions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;
using FluentAssertions;
using Xunit;

public class TypedObjectDictionaryValueTests
{
    public static TheoryData<string, ushort, object> TypedValues => new()
    {
        { "1", 0x0001, true },
        { "true", 0x0001, true },
        { "0", 0x0001, false },
        { "false", 0x0001, false },
        { "0xFF", 0x0002, (sbyte)-1 },
        { "017", 0x0002, (sbyte)15 },
        { "0377", 0x0002, (sbyte)-1 },
        { "-1234", 0x0003, (short)-1234 },
        { "0xFFFFFFFF", 0x0004, -1 },
        { "255", 0x0005, (byte)255 },
        { "0377", 0x0005, (byte)255 },
        { "0x1234", 0x0006, (ushort)0x1234 },
        { "0x12345678", 0x0007, 0x12345678U },
        { "1.25", 0x0008, 1.25F },
        { "Device name", 0x0009, "Device name" },
        { "Grüße", 0x000B, "Grüße" },
        { "0xFF00FF", 0x0010, -65281 },
        { "-8388608", 0x0010, -8388608 },
        { "1.25", 0x0011, 1.25D },
        { "-549755813888", 0x0012, -549755813888L },
        { "0xFFFFFFFFFFFF", 0x0013, -1L },
        { "0xFFFFFFFFFFFFFF", 0x0014, -1L },
        { "9223372036854775807", 0x0015, long.MaxValue },
        { "0xFFFFFFFFFFFFFFFF", 0x0015, -1L },
        { "0777", 0x0015, 511L },
        { "16777215", 0x0016, 16777215U },
        { "0xFFFFFFFFFF", 0x0018, 1099511627775UL },
        { "281474976710655", 0x0019, 281474976710655UL },
        { "0x1AB", 0x001A, 427UL },
        { "18446744073709551615", 0x001B, ulong.MaxValue }
    };

    [Theory]
    [MemberData(nameof(TypedValues))]
    public void Parse_SupportedDataType_ReturnsExpectedDotNetType(string value, ushort dataType, object expected)
    {
        var result = CanOpenValueConverter.Parse(value, dataType);

        result.Should().Be(expected);
        result.GetType().Should().Be(expected.GetType());
    }

    [Fact]
    public void Parse_OctetString_ReturnsByteArray()
    {
        CanOpenValueConverter.Parse("0x0102FE", 0x000A)
            .Should().BeEquivalentTo(new byte[] { 0x01, 0x02, 0xFE });
    }

    [Fact]
    public void Parse_ByteStringWithSeparators_IgnoresSpacesAndDashes()
    {
        CanOpenValueConverter.Parse("01 02-fe", 0x000F)
            .Should().BeEquivalentTo(new byte[] { 0x01, 0x02, 0xFE });
    }

    [Theory]
    [InlineData("0x123", 0x000A)]  // odd digit count
    [InlineData("0xZZ", 0x000A)]   // invalid hex digit
    [InlineData("0x!!", 0x000A)]   // character below '0'
    [InlineData("0x::", 0x000A)]   // character between '9' and 'A'
    [InlineData("maybe", 0x0001)]  // invalid boolean token
    public void Parse_MalformedValue_ThrowsFormatException(string value, ushort dataType)
    {
        var act = () => CanOpenValueConverter.Parse(value, dataType);

        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("256", 0x0005)]           // unsigned decimal overflow
    [InlineData("0x1FF", 0x0005)]         // unsigned hex overflow
    [InlineData("128", 0x0002)]           // signed decimal overflow
    [InlineData("-129", 0x0002)]          // signed decimal underflow
    [InlineData("0x1000000", 0x0010)]     // signed hex overflow (24-bit)
    public void Parse_ValueOutsideRange_ThrowsOverflowException(string value, ushort dataType)
    {
        var act = () => CanOpenValueConverter.Parse(value, dataType);

        act.Should().Throw<OverflowException>();
    }

    [Theory]
    [InlineData(0x000C)] // TIME_OF_DAY
    [InlineData(0x000D)] // TIME_DIFFERENCE
    public void ParseAndFormat_TimeTypes_ThrowNotSupportedException(ushort dataType)
    {
        var parse = () => CanOpenValueConverter.Parse("1", dataType);
        var format = () => CanOpenValueConverter.Format(1, dataType);

        parse.Should().Throw<NotSupportedException>().WithMessage($"*0x{dataType:X4}*");
        format.Should().Throw<NotSupportedException>().WithMessage($"*0x{dataType:X4}*");
    }

    [Fact]
    public void ParseAndFormat_NullValue_ThrowArgumentNullException()
    {
        var parse = () => CanOpenValueConverter.Parse(null!, 0x0005);
        var format = () => CanOpenValueConverter.Format(null!, 0x0005);

        parse.Should().Throw<ArgumentNullException>();
        format.Should().Throw<ArgumentNullException>();
    }

    public static TheoryData<object, ushort, string> FormattedValues => new()
    {
        { false, 0x0001, "0" },
        { (sbyte)-5, 0x0002, "-5" },
        { (short)-1234, 0x0003, "-1234" },
        { -70000, 0x0004, "-70000" },
        { (byte)7, 0x0005, "7" },
        { (ushort)512, 0x0006, "512" },
        { 0x12345678U, 0x0007, "305419896" },
        { "Device", 0x0009, "Device" },
        { "Grüße", 0x000B, "Grüße" },
        { -8388608, 0x0010, "-8388608" },
        { 2.5D, 0x0011, "2.5" },
        { -549755813888L, 0x0012, "-549755813888" },
        { -140737488355328L, 0x0013, "-140737488355328" },
        { -36028797018963968L, 0x0014, "-36028797018963968" },
        { long.MinValue, 0x0015, "-9223372036854775808" },
        { 16777215U, 0x0016, "16777215" },
        { 1099511627775UL, 0x0018, "1099511627775" },
        { 281474976710655UL, 0x0019, "281474976710655" },
        { 72057594037927935UL, 0x001A, "72057594037927935" },
        { ulong.MaxValue, 0x001B, "18446744073709551615" }
    };

    [Theory]
    [MemberData(nameof(FormattedValues))]
    public void Format_SupportedDataType_ProducesCanonicalString(object value, ushort dataType, string expected)
    {
        CanOpenValueConverter.Format(value, dataType).Should().Be(expected);
    }

    [Fact]
    public void Format_SignedValueOutsideRange_ThrowsArgumentException()
    {
        var act = () => CanOpenValueConverter.Format(128, 0x0002);

        act.Should().Throw<ArgumentException>().WithMessage("*0x0002*");
    }

    [Fact]
    public void Format_NonByteArrayForOctetString_ThrowsArgumentException()
    {
        var act = () => CanOpenValueConverter.Format("not bytes", 0x000A);

        act.Should().Throw<ArgumentException>().WithMessage("*0x000A*");
    }

    [Fact]
    public void Format_UnsupportedDataType_ThrowsNotSupportedException()
    {
        var act = () => CanOpenValueConverter.Format(1, 0x0020);

        act.Should().Throw<NotSupportedException>().WithMessage("*0x0020*");
    }

    [Fact]
    public void Format_UsesInvariantAndCanonicalRepresentations()
    {
        CanOpenValueConverter.Format(true, 0x0001).Should().Be("1");
        CanOpenValueConverter.Format(1.5F, 0x0008).Should().Be("1.5");
        CanOpenValueConverter.Format(new byte[] { 0x01, 0xAB }, 0x000F).Should().Be("0x01AB");
    }

    [Fact]
    public void Format_ValueOutsideDataTypeRange_ThrowsArgumentException()
    {
        var act = () => CanOpenValueConverter.Format(256, 0x0005);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*0x0005*");
    }

    [Fact]
    public void Parse_UnsupportedDataType_ThrowsNotSupportedException()
    {
        var act = () => CanOpenValueConverter.Parse("1", 0x0020);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*0x0020*");
    }

    [Fact]
    public void GetParameterValueAsObject_UsesConfiguredValueBeforeDefaultAndDataType()
    {
        var dictionary = CreateDictionary();
        dictionary.Objects[0x2000].ParameterValue = "0x2A";

        var result = dictionary.GetParameterValueAsObject(0x2000);

        result.Should().BeOfType<byte>().Which.Should().Be(42);
    }

    [Fact]
    public void GetParameterValue_Generic_ReturnsStronglyTypedValue()
    {
        var dictionary = CreateDictionary();

        dictionary.GetParameterValue<uint>(0x1018, 1).Should().Be(0x100U);
    }

    [Fact]
    public void GetParameterValueAsObject_SubObjectConfiguredValue_TakesPrecedenceOverDefault()
    {
        var dictionary = CreateDictionary();
        dictionary.Objects[0x1018].SubObjects[1].ParameterValue = "0x200";

        dictionary.GetParameterValueAsObject(0x1018, 1).Should().Be(0x200U);
    }

    [Fact]
    public void GetParameterValue_GenericWithWrongType_ThrowsHelpfulException()
    {
        var dictionary = CreateDictionary();

        var act = () => dictionary.GetParameterValue<int>(0x2000);

        act.Should().Throw<InvalidCastException>()
            .WithMessage("*0x2000*Byte*Int32*");
    }

    [Fact]
    public void SetParameterValue_Object_FormatsAccordingToObjectDataType()
    {
        var dictionary = CreateDictionary();

        dictionary.SetParameterValue(0x2000, (object)(byte)42).Should().BeTrue();

        dictionary.Objects[0x2000].ParameterValue.Should().Be("42");
        dictionary.GetParameterValue<byte>(0x2000).Should().Be(42);
    }

    [Fact]
    public void SetParameterValue_ObjectForSubObject_UsesSubObjectDataType()
    {
        var dictionary = CreateDictionary();

        dictionary.SetParameterValue(0x1018, 1, (object)0x12345678U).Should().BeTrue();

        dictionary.Objects[0x1018].SubObjects[1].ParameterValue.Should().Be("305419896");
    }

    [Fact]
    public void SetParameterValue_StringOverload_RemainsLosslessAndBackwardCompatible()
    {
        var dictionary = CreateDictionary();

        dictionary.SetParameterValue(0x2000, "0x2A").Should().BeTrue();

        dictionary.Objects[0x2000].ParameterValue.Should().Be("0x2A");
    }

    [Fact]
    public void TypedAccess_ObjectWithoutDataType_ThrowsInvalidOperationException()
    {
        var dictionary = CreateDictionary();
        dictionary.Objects[0x3000] = new CanOpenObject
        {
            Index = 0x3000,
            DefaultValue = "1"
        };

        var get = () => dictionary.GetParameterValueAsObject(0x3000);
        var set = () => dictionary.SetParameterValue(0x3000, (object)1);

        get.Should().Throw<InvalidOperationException>().WithMessage("*0x3000*");
        set.Should().Throw<InvalidOperationException>().WithMessage("*0x3000*");
    }

    [Fact]
    public void TypedAccess_SubObjectWithoutDataType_ThrowsInvalidOperationException()
    {
        var dictionary = CreateDictionary();
        dictionary.Objects[0x1400] = new CanOpenObject
        {
            Index = 0x1400,
            ObjectType = 0x09
        };
        dictionary.Objects[0x1400].SubObjects[1] = new CanOpenSubObject
        {
            SubIndex = 1,
            DataType = 0,
            DefaultValue = "$NODEID+0x200"
        };

        var act = () => dictionary.GetParameterValueAsObject(0x1400, 1);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*0x1400:01*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyOrWhitespaceInteger_ReturnsZero(string value)
    {
        CanOpenValueConverter.Parse(value, 0x0004).Should().Be(0);
        CanOpenValueConverter.Parse(value, 0x0007).Should().Be(0U);
        CanOpenValueConverter.Parse(value, 0x0005).Should().Be((byte)0);
    }

    [Fact]
    public void Parse_NodeIdFormula_EvaluatesWithProvidedNodeId()
    {
        CanOpenValueConverter.Parse("$NODEID+0x200", 0x0007, nodeId: 5)
            .Should().Be(517U);
        CanOpenValueConverter.Parse("$NODEID", 0x0005, nodeId: 7)
            .Should().Be((byte)7);
    }

    [Fact]
    public void Parse_NodeIdFormulaWithoutNodeId_ThrowsNotSupportedException()
    {
        var act = () => CanOpenValueConverter.Parse("$NODEID+0x200", 0x0007);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Cannot evaluate $NODEID formula*");
    }

    [Fact]
    public void GetParameterValue_SubObjectNodeIdFormula_EvaluatesWithNodeId()
    {
        var dictionary = CreateDictionary();
        dictionary.Objects[0x1400] = new CanOpenObject
        {
            Index = 0x1400,
            ObjectType = 0x09
        };
        dictionary.Objects[0x1400].SubObjects[1] = new CanOpenSubObject
        {
            SubIndex = 1,
            DataType = 0x0007,
            DefaultValue = "$NODEID+0x200"
        };

        dictionary.GetParameterValueAsObject(0x1400, 1, nodeId: 5).Should().Be(517U);
        dictionary.GetParameterValue<uint>(0x1400, 1, nodeId: 5).Should().Be(517U);
        dictionary.GetParameterValue(0x1400, 1).Should().Be("$NODEID+0x200");
    }

    [Fact]
    public void TypedAccess_MissingObjectOrValue_ReturnsNullOrFalse()
    {
        var dictionary = CreateDictionary();
        dictionary.Objects[0x3000] = new CanOpenObject { Index = 0x3000, DataType = 0x0005 };

        dictionary.GetParameterValueAsObject(0x9999).Should().BeNull();
        dictionary.GetParameterValueAsObject(0x3000).Should().BeNull();
        dictionary.SetParameterValue(0x9999, (object)1).Should().BeFalse();
    }

    [Fact]
    public void TypedAccess_MissingSubObjectOrValue_ReturnsNullOrFalse()
    {
        var dictionary = CreateDictionary();
        dictionary.Objects[0x1018].SubObjects[2] = new CanOpenSubObject { SubIndex = 2, DataType = 0x0005 };

        dictionary.GetParameterValueAsObject(0x1018, 99).Should().BeNull();
        dictionary.GetParameterValueAsObject(0x1018, 2).Should().BeNull();
        dictionary.SetParameterValue(0x1018, 99, (object)1U).Should().BeFalse();
    }

    [Fact]
    public void SetParameterValue_NullTypedValue_ThrowsArgumentNullException()
    {
        var dictionary = CreateDictionary();

        var forObject = () => dictionary.SetParameterValue(0x2000, (object)null!);
        var forSubObject = () => dictionary.SetParameterValue(0x1018, 1, (object)null!);

        forObject.Should().Throw<ArgumentNullException>();
        forSubObject.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Parse_NodeIdFormulaForSignedType_EvaluatesWithNodeId()
    {
        CanOpenValueConverter.Parse("$NODEID+0x100", 0x0004, nodeId: 2).Should().Be(0x102);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-1)]
    public void Format_NumericBooleanOutsideRange_ThrowsArgumentException(int value)
    {
        var act = () => CanOpenValueConverter.Format(value, 0x0001);

        act.Should().Throw<ArgumentException>().WithMessage("*0x0001*");
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    public void Format_NumericBooleanZeroOrOne_IsAccepted(int value, string expected)
    {
        CanOpenValueConverter.Format(value, 0x0001).Should().Be(expected);
    }

    public static TheoryData<object, ushort> FractionalValues => new()
    {
        { 1.5D, 0x0003 },  // double -> INTEGER16
        { 1.5F, 0x0003 },  // float -> INTEGER16
        { 1.5M, 0x0003 },  // decimal -> INTEGER16
        { 1.5D, 0x0007 },  // double -> UNSIGNED32
        { 1.5F, 0x0007 },  // float -> UNSIGNED32
        { 1.5M, 0x0007 }   // decimal -> UNSIGNED32
    };

    [Theory]
    [MemberData(nameof(FractionalValues))]
    public void Format_FractionalValueForIntegerType_ThrowsArgumentException(object value, ushort dataType)
    {
        var act = () => CanOpenValueConverter.Format(value, dataType);

        act.Should().Throw<ArgumentException>().WithMessage($"*0x{dataType:X4}*");
    }

    [Theory]
    [InlineData(0x0009)] // VISIBLE_STRING
    [InlineData(0x000B)] // UNICODE_STRING
    public void Format_NonStringForStringType_ThrowsArgumentException(ushort dataType)
    {
        var act = () => CanOpenValueConverter.Format(new byte[] { 0x01 }, dataType);

        act.Should().Throw<ArgumentException>().WithMessage($"*0x{dataType:X4}*");
    }

    [Fact]
    public void SetParameterValue_SubObjectWithoutDataType_ThrowsInvalidOperationException()
    {
        var dictionary = CreateDictionary();
        dictionary.Objects[0x1018].SubObjects[3] = new CanOpenSubObject { SubIndex = 3, DataType = 0 };

        var act = () => dictionary.SetParameterValue(0x1018, 3, (object)1U);

        act.Should().Throw<InvalidOperationException>().WithMessage("*0x1018:03*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetParameterValueAsObject_EmptyDefault_TreatedAsMissingValue(string emptyDefault)
    {
        var dictionary = CreateDictionary();
        dictionary.Objects[0x2000].DefaultValue = emptyDefault;
        dictionary.Objects[0x1018].SubObjects[1].DefaultValue = emptyDefault;

        dictionary.GetParameterValueAsObject(0x2000).Should().BeNull();
        dictionary.GetParameterValueAsObject(0x1018, 1).Should().BeNull();
    }

    [Fact]
    public void SetParameterValue_NullLiteral_StillBindsToStringOverload()
    {
        var dictionary = CreateDictionary();
        dictionary.Objects[0x2000].ParameterValue = "10";

        // Overload resolution must keep preferring the string overload for null literals,
        // preserving source compatibility with pre-typed-API callers.
        dictionary.SetParameterValue(0x2000, null!).Should().BeTrue();

        dictionary.Objects[0x2000].ParameterValue.Should().BeNull();
    }

    [Fact]
    public void GetParameterValue_GenericMissingValue_ThrowsKeyNotFoundException()
    {
        var dictionary = CreateDictionary();

        var act = () => dictionary.GetParameterValue<byte>(0x9999);

        act.Should().Throw<KeyNotFoundException>().WithMessage("*0x9999*");
    }

    private static ObjectDictionary CreateDictionary()
    {
        var dictionary = new ObjectDictionary();
        dictionary.Objects[0x2000] = new CanOpenObject
        {
            Index = 0x2000,
            DataType = 0x0005,
            DefaultValue = "10"
        };
        dictionary.Objects[0x1018] = new CanOpenObject
        {
            Index = 0x1018,
            ObjectType = 0x09
        };
        dictionary.Objects[0x1018].SubObjects[1] = new CanOpenSubObject
        {
            SubIndex = 1,
            DataType = 0x0007,
            DefaultValue = "0x100"
        };
        return dictionary;
    }
}
