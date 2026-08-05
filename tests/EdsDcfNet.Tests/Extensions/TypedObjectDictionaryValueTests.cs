namespace EdsDcfNet.Tests.Extensions;

using System.Globalization;
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
        { "yes", 0x0001, true },
        { "YES", 0x0001, true },
        { "0", 0x0001, false },
        { "false", 0x0001, false },
        { "no", 0x0001, false },
        { "No", 0x0001, false },
        { "0xFF", 0x0002, (sbyte)-1 },
        { "017", 0x0002, (sbyte)17 },
        { "08", 0x0002, (sbyte)8 },
        { "-1234", 0x0003, (short)-1234 },
        { "0xFFFFFFFF", 0x0004, -1 },
        { "255", 0x0005, (byte)255 },
        { "0255", 0x0005, (byte)255 },
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
        { "0511", 0x0015, 511L },
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
        CanOpenValueConverter.Parse("01 02-fe", 0x000A)
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
        CanOpenValueConverter.Format(new byte[] { 0x01, 0xAB }, 0x000A).Should().Be("0x01AB");
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
    public void TypedAccess_ObjectWithDataTypeZero_ThrowsInvalidOperationException()
    {
        var dictionary = CreateDictionary();
        dictionary.Objects[0x3001] = new CanOpenObject
        {
            Index = 0x3001,
            DataType = 0, // parser sentinel for an omitted DataType field
            DefaultValue = "1"
        };

        var get = () => dictionary.GetParameterValueAsObject(0x3001);
        var set = () => dictionary.SetParameterValue(0x3001, (object)1);

        get.Should().Throw<InvalidOperationException>().WithMessage("*0x3001*");
        set.Should().Throw<InvalidOperationException>().WithMessage("*0x3001*");
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

    [Fact]
    public void Parse_NodeIdSubtractionForSignedType_YieldsNegativeValue()
    {
        CanOpenValueConverter.Parse("$NODEID-2", 0x0002, nodeId: 1).Should().Be((sbyte)-1);
        CanOpenValueConverter.Parse("$NODEID-0x10", 0x0004, nodeId: 2).Should().Be(-14);
    }

    [Fact]
    public void Parse_NodeIdSubtractionOutsideSignedRange_ThrowsOverflowException()
    {
        var act = () => CanOpenValueConverter.Parse("$NODEID-200", 0x0002, nodeId: 1);

        act.Should().Throw<OverflowException>();
    }

    [Fact]
    public void Parse_SignedNodeIdFormulaWithoutNodeId_ThrowsNotSupportedException()
    {
        var act = () => CanOpenValueConverter.Parse("$NODEID+1", 0x0004);

        act.Should().Throw<NotSupportedException>().WithMessage("*node ID*");
    }

    [Theory]
    [InlineData("$NODEID+")]
    [InlineData("$NODEID*2")]
    [InlineData("$NODEID+1+1")]
    public void Parse_MalformedSignedNodeIdFormula_ThrowsFormatException(string formula)
    {
        var act = () => CanOpenValueConverter.Parse(formula, 0x0004, nodeId: 1);

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_BareNodeIdTokenForSignedType_ReturnsNodeId()
    {
        CanOpenValueConverter.Parse("$NODEID", 0x0004, nodeId: 5).Should().Be(5);
    }

    [Fact]
    public void Parse_NodeIdFormulaWithLargeOffset_SupportsWideUnsignedTypes()
    {
        // Offsets beyond the 32-bit range must survive for UNSIGNED40..UNSIGNED64.
        CanOpenValueConverter.Parse("$NODEID+0x100000000", 0x001B, nodeId: 2).Should().Be(0x100000002UL);
        CanOpenValueConverter.Parse("$NODEID+0xFF00000000", 0x0018, nodeId: 1).Should().Be(0xFF00000001UL);
    }

    [Fact]
    public void Parse_NodeIdFormulaWithLargeOffset_SupportsWideSignedTypes()
    {
        CanOpenValueConverter.Parse("$NODEID+0x100000000", 0x0015, nodeId: 2).Should().Be(0x100000002L);
        CanOpenValueConverter.Parse("$NODEID-0x100000000", 0x0015, nodeId: 1).Should().Be(-0xFFFFFFFFL);
    }

    [Fact]
    public void Parse_UnsignedNodeIdSubtractionBelowZero_ThrowsOverflowException()
    {
        var act = () => CanOpenValueConverter.Parse("$NODEID-2", 0x0005, nodeId: 1);

        act.Should().Throw<OverflowException>();
    }

    [Fact]
    public void Parse_UnsignedNodeIdSubtractionWithinRange_ReturnsDifference()
    {
        CanOpenValueConverter.Parse("$NODEID-2", 0x0005, nodeId: 10).Should().Be((byte)8);
    }

    [Theory]
    [InlineData(0x0005)] // UNSIGNED8
    [InlineData(0x0004)] // INTEGER16
    public void Format_BooleanForIntegerType_ThrowsInvalidCastException(ushort dataType)
    {
        // BOOLEAN is a separate CANopen data type; true must not silently become 1.
        var act = () => CanOpenValueConverter.Format(true, dataType);

        act.Should().Throw<ArgumentException>().WithInnerException<InvalidCastException>().WithMessage("*integral*");
    }

    [Theory]
    [InlineData(0x0005)]
    [InlineData(0x0003)]
    public void Format_CharForIntegerType_ThrowsInvalidCastException(ushort dataType)
    {
        var act = () => CanOpenValueConverter.Format('1', dataType);

        act.Should().Throw<ArgumentException>().WithInnerException<InvalidCastException>().WithMessage("*integral*");
    }

    [Fact]
    public void SetParameterValue_BooleanForUnsignedObject_ThrowsInvalidCastException()
    {
        var dictionary = CreateDictionary();

        var act = () => dictionary.SetParameterValue(0x2000, (object)true);

        act.Should().Throw<ArgumentException>().WithInnerException<InvalidCastException>();
    }

    [Fact]
    public void Parse_UnsignedNodeIdFormulaWithoutNodeId_ThrowsNotSupportedException()
    {
        var act = () => CanOpenValueConverter.Parse("$NODEID+1", 0x0007);

        act.Should().Throw<NotSupportedException>().WithMessage("*node ID*");
    }

    [Fact]
    public void Parse_BareNodeIdTokenForUnsignedType_ReturnsNodeId()
    {
        CanOpenValueConverter.Parse("$NODEID", 0x0007, nodeId: 9).Should().Be(9U);
    }

    [Theory]
    [InlineData("$NODEID-")]
    [InlineData("$NODEID/2")]
    [InlineData("$NODEID+abc")]
    [InlineData("$NODEID+1-1")]
    public void Parse_MalformedUnsignedNodeIdFormula_ThrowsFormatException(string formula)
    {
        // The typed converter must consistently throw FormatException, never EdsParseException.
        var act = () => CanOpenValueConverter.Parse(formula, 0x0007, nodeId: 1);

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_SignedNodeIdOperandOutsideSigned64Range_ThrowsOverflowException()
    {
        // long.MinValue magnitude is 2^63; one more than that underflows.
        var act = () => CanOpenValueConverter.Parse("$NODEID-0x8000000000000001", 0x0015, nodeId: 0);

        act.Should().Throw<OverflowException>();
    }

    [Fact]
    public void Parse_SignedNodeIdSubtractionWithOperandAboveInt64Max_ReturnsNegativeResult()
    {
        // 1 - 0x8000000000000000 == -9223372036854775807, which fits in INTEGER64.
        CanOpenValueConverter.Parse("$NODEID-0x8000000000000000", 0x0015, nodeId: 1)
            .Should().Be(-9223372036854775807L);
    }

    [Fact]
    public void Parse_SignedNodeIdSubtractionReachingInt64Min_ReturnsMinValue()
    {
        CanOpenValueConverter.Parse("$NODEID-0x8000000000000000", 0x0015, nodeId: 0)
            .Should().Be(long.MinValue);
    }

    [Fact]
    public void Parse_SignedNodeIdSubtractionStayingPositive_ReturnsDifference()
    {
        CanOpenValueConverter.Parse("$NODEID-2", 0x0003, nodeId: 10).Should().Be((short)8);
    }

    [Theory]
    [InlineData(0x000F)]
    public void Parse_DomainDataType_ThrowsNotSupportedException(ushort dataType)
    {
        // DCF DOMAIN payloads live in UploadFile/DownloadFile, not in ParameterValue.
        var act = () => CanOpenValueConverter.Parse("0x01AB", dataType);

        act.Should().Throw<NotSupportedException>().WithMessage("*DOMAIN*");
    }

    [Fact]
    public void Format_DomainDataType_ThrowsNotSupportedException()
    {
        var act = () => CanOpenValueConverter.Format(new byte[] { 0x01, 0xAB }, 0x000F);

        act.Should().Throw<NotSupportedException>().WithMessage("*DOMAIN*");
    }

    [Theory]
    [InlineData(0x0008)] // REAL32
    [InlineData(0x0011)] // REAL64
    public void Format_BooleanForRealType_ThrowsInvalidCastException(ushort dataType)
    {
        // BOOLEAN is a separate CANopen data type; true must not silently become 1.
        var act = () => CanOpenValueConverter.Format(true, dataType);

        act.Should().Throw<ArgumentException>().WithInnerException<InvalidCastException>().WithMessage("*numeric*");
    }

    [Theory]
    [InlineData(0x0008)]
    [InlineData(0x0011)]
    public void Format_CharForRealType_ThrowsInvalidCastException(ushort dataType)
    {
        var act = () => CanOpenValueConverter.Format('1', dataType);

        act.Should().Throw<ArgumentException>().WithInnerException<InvalidCastException>().WithMessage("*numeric*");
    }

    public static TheoryData<object> NumericRealInputs => new()
    {
        (sbyte)1,
        (byte)2,
        (short)3,
        (ushort)4,
        5,
        6U,
        7L,
        8UL,
        9.5F,
        10.5D,
        11.5M
    };

    [Theory]
    [MemberData(nameof(NumericRealInputs))]
    public void Format_NumericValuesForRealType_AreAccepted(object value)
    {
        var act = () => CanOpenValueConverter.Format(value, 0x0008);

        act.Should().NotThrow();
        CanOpenValueConverter.Format(value, 0x0011).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SetParameterValue_BooleanForRealObject_ThrowsInvalidCastException()
    {
        var dictionary = CreateDictionary();
        dictionary.Objects[0x2003] = new CanOpenObject { Index = 0x2003, DataType = 0x0008 };

        var act = () => dictionary.SetParameterValue(0x2003, (object)true);

        act.Should().Throw<ArgumentException>().WithInnerException<InvalidCastException>();
    }

    [Fact]
    public void Parse_SignedNodeIdAdditionOverflowingInt64_ThrowsOverflowException()
    {
        var act = () => CanOpenValueConverter.Parse("$NODEID+0x7FFFFFFFFFFFFFFF", 0x0015, nodeId: 1);

        act.Should().Throw<OverflowException>();
    }

    [Fact]
    public void Parse_UnsignedNodeIdAdditionOverflowingUInt64_ThrowsOverflowException()
    {
        var act = () => CanOpenValueConverter.Parse("$NODEID+0xFFFFFFFFFFFFFFFF", 0x001B, nodeId: 1);

        act.Should().Throw<OverflowException>();
    }

    [Fact]
    public void GetParameterValue_NodeIdPassedByName_ReadsObjectLevelValue()
    {
        var dictionary = CreateDictionary();
        dictionary.Objects[0x2002] = new CanOpenObject
        {
            Index = 0x2002,
            DataType = 0x0007,
            DefaultValue = "$NODEID+0x200"
        };

        // The second positional argument is always the sub-index, so nodeId must be named.
        dictionary.GetParameterValue<uint>(0x2002, nodeId: 5).Should().Be(0x205U);
        dictionary.GetParameterValueAsObject(0x2002, nodeId: 5).Should().Be(0x205U);
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

    public static TheoryData<object> FractionalBooleanValues => new() { 0.25D, 1.4F, 0.5M };

    [Theory]
    [MemberData(nameof(FractionalBooleanValues))]
    public void Format_FractionalNumericBoolean_ThrowsArgumentException(object value)
    {
        var act = () => CanOpenValueConverter.Format(value, 0x0001);

        act.Should().Throw<ArgumentException>().WithMessage("*0x0001*");
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetParameterValueAsObject_BlankParameterValue_FallsBackToDefault(string blankParameter)
    {
        var dictionary = CreateDictionary();
        dictionary.Objects[0x2000].ParameterValue = blankParameter;
        dictionary.Objects[0x2000].DefaultValue = "10";
        dictionary.Objects[0x1018].SubObjects[1].ParameterValue = blankParameter;
        dictionary.Objects[0x1018].SubObjects[1].DefaultValue = "20";

        dictionary.GetParameterValueAsObject(0x2000).Should().Be((byte)10);
        dictionary.GetParameterValueAsObject(0x1018, 1).Should().Be(20U);
    }

    [Fact]
    public void GetParameterValue_ParsedDcfWithoutConfiguredValue_FallsBackToDefault()
    {
        // End-to-end: DcfReader stores an absent ParameterValue INI key as an empty
        // string, which must not mask the DefaultValue for typed reads.
        const string dcfContent = """
            [FileInfo]
            FileName=test.dcf
            FileVersion=1
            FileRevision=0
            EDSVersion=4.0

            [DeviceInfo]
            VendorName=Test
            VendorNumber=0x1
            ProductName=Test Device
            ProductNumber=0x1
            RevisionNumber=0x1
            OrderCode=T-1
            BaudRate_500=1
            SimpleBootUpSlave=1
            Granularity=8
            NrOfRXPDO=0
            NrOfTXPDO=0
            LSS_Supported=0

            [DeviceComissioning]
            NodeID=2
            NodeName=TestNode
            Baudrate=500
            NetNumber=1
            NetworkName=net
            LSS_SerialNumber=0

            [MandatoryObjects]
            SupportedObjects=1
            1=0x1000

            [1000]
            ParameterName=Device Type
            ObjectType=0x7
            DataType=0x0007
            AccessType=ro
            DefaultValue=0x00000191
            PDOMapping=0
            """;

        var dcf = CanOpenFile.Dcf.ReadString(dcfContent);

        dcf.ObjectDictionary.GetParameterValue<uint>(0x1000).Should().Be(0x191U);
    }

    [Fact]
    public void GetParameterValueAsObject_WhitespaceOnlyStringValue_IsPreserved()
    {
        var dictionary = CreateDictionary();
        dictionary.Objects[0x2001] = new CanOpenObject
        {
            Index = 0x2001,
            DataType = 0x0009, // VISIBLE_STRING
            ParameterValue = "   "
        };
        dictionary.Objects[0x1018].SubObjects[4] = new CanOpenSubObject
        {
            SubIndex = 4,
            DataType = 0x000B, // UNICODE_STRING
            DefaultValue = "  "
        };

        dictionary.GetParameterValueAsObject(0x2001).Should().Be("   ");
        dictionary.GetParameterValue<string>(0x2001).Should().Be("   ");
        dictionary.GetParameterValueAsObject(0x1018, 4).Should().Be("  ");
    }

    [Fact]
    public void GetParameterValueAsObject_EmptyStringTypeValue_TreatedAsMissing()
    {
        var dictionary = CreateDictionary();
        dictionary.Objects[0x2001] = new CanOpenObject
        {
            Index = 0x2001,
            DataType = 0x0009, // VISIBLE_STRING
            DefaultValue = ""  // parser default for a missing value
        };

        dictionary.GetParameterValueAsObject(0x2001).Should().BeNull();
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

    [Theory]
    [InlineData(0x0008)] // REAL32
    [InlineData(0x0011)] // REAL64
    public void Format_PositiveInfinityForRealType_ThrowsArgumentException(ushort dataType)
    {
        var act = () => CanOpenValueConverter.Format(double.PositiveInfinity, dataType);

        act.Should().Throw<ArgumentException>().WithInnerException<OverflowException>();
    }

    [Theory]
    [InlineData(0x0008)]
    [InlineData(0x0011)]
    public void Format_NegativeInfinityForRealType_ThrowsArgumentException(ushort dataType)
    {
        var act = () => CanOpenValueConverter.Format(double.NegativeInfinity, dataType);

        act.Should().Throw<ArgumentException>().WithInnerException<OverflowException>();
    }

    [Theory]
    [InlineData(0x0008)]
    [InlineData(0x0011)]
    public void Format_NaNForRealType_ThrowsArgumentException(ushort dataType)
    {
        var act = () => CanOpenValueConverter.Format(double.NaN, dataType);

        act.Should().Throw<ArgumentException>().WithInnerException<FormatException>();
    }

    public static TheoryData<object> Real32OverflowingInputs => new()
    {
        double.MaxValue,
        double.MinValue,
        3.5e40D,
        -3.5e40D
    };

    [Theory]
    [MemberData(nameof(Real32OverflowingInputs))]
    public void Format_WiderValueOutsideReal32Range_ThrowsArgumentException(object value)
    {
        // Convert.ToSingle saturates to infinity instead of throwing, so the converted
        // result must be validated explicitly (see PR #393 review feedback).
        var act = () => CanOpenValueConverter.Format(value, 0x0008);

        act.Should().Throw<ArgumentException>()
            .WithInnerException<OverflowException>()
            .WithMessage("*REAL32*");
    }

    [Fact]
    public void Format_Real32AtMaxValue_IsAccepted()
    {
        CanOpenValueConverter.Format(float.MaxValue, 0x0008)
            .Should().Be(float.MaxValue.ToString("R", CultureInfo.InvariantCulture));
        CanOpenValueConverter.Format(float.MinValue, 0x0008)
            .Should().Be(float.MinValue.ToString("R", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Format_Real64AtMaxValue_IsAccepted()
    {
        CanOpenValueConverter.Format(double.MaxValue, 0x0011)
            .Should().Be(double.MaxValue.ToString("R", CultureInfo.InvariantCulture));
        CanOpenValueConverter.Format(double.MinValue, 0x0011)
            .Should().Be(double.MinValue.ToString("R", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void SetParameterValue_Real32OverflowingValue_DoesNotPersistInfinity()
    {
        var dictionary = CreateDictionary();
        dictionary.Objects[0x2004] = new CanOpenObject
        {
            Index = 0x2004,
            DataType = 0x0008,
            ParameterValue = "1.5"
        };

        var act = () => dictionary.SetParameterValue(0x2004, (object)double.MaxValue);

        act.Should().Throw<ArgumentException>();
        dictionary.Objects[0x2004].ParameterValue.Should().Be("1.5");
    }

    [Theory]
    [InlineData("3.5e40", 0x0008)]   // saturates float.Parse to +Infinity
    [InlineData("-3.5e40", 0x0008)]  // saturates float.Parse to -Infinity
    [InlineData("3.5e400", 0x0011)]  // saturates double.Parse to +Infinity
    [InlineData("-3.5e400", 0x0011)]
    public void Parse_RealLiteralOutsideRange_ThrowsOverflowException(string value, ushort dataType)
    {
        // float.Parse/double.Parse saturate to infinity rather than throwing, so a value that
        // cannot be represented must not silently round-trip as Infinity.
        var act = () => CanOpenValueConverter.Parse(value, dataType);

        act.Should().Throw<OverflowException>();
    }

    [Theory]
    [InlineData("NaN", 0x0008)]
    [InlineData("NaN", 0x0011)]
    [InlineData("Infinity", 0x0008)]
    [InlineData("-Infinity", 0x0011)]
    public void Parse_NonFiniteRealLiteral_ThrowsFormatOrOverflowException(string value, ushort dataType)
    {
        var act = () => CanOpenValueConverter.Parse(value, dataType);

        act.Should().Throw<Exception>().Which.Should().Match(ex => ex is FormatException || ex is OverflowException);
    }

    [Fact]
    public void Parse_RealAtMaxValue_RoundTripsThroughFormat()
    {
        var real32 = CanOpenValueConverter.Format(float.MaxValue, 0x0008);
        CanOpenValueConverter.Parse(real32, 0x0008).Should().Be(float.MaxValue);

        var real64 = CanOpenValueConverter.Format(double.MaxValue, 0x0011);
        CanOpenValueConverter.Parse(real64, 0x0011).Should().Be(double.MaxValue);
    }

    [Fact]
    public void Format_NonByteArrayForOctetString_MessageDoesNotMentionDomain()
    {
        // DOMAIN is rejected earlier with NotSupportedException, so naming it here would
        // mislead callers debugging a type mismatch (see PR #393 review feedback).
        var act = () => CanOpenValueConverter.Format("not bytes", 0x000A);

        var inner = act.Should().Throw<ArgumentException>()
            .WithInnerException<InvalidCastException>().Which;
        inner.Message.Should().Contain("OCTET_STRING");
        inner.Message.Should().Contain("String");
        inner.Message.Should().NotContain("DOMAIN");
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
