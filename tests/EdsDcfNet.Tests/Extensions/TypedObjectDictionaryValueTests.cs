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
        { "0xFF", 0x0002, (sbyte)-1 },
        { "-1234", 0x0003, (short)-1234 },
        { "0xFFFFFFFF", 0x0004, -1 },
        { "255", 0x0005, (byte)255 },
        { "0x1234", 0x0006, (ushort)0x1234 },
        { "0x12345678", 0x0007, 0x12345678U },
        { "1.25", 0x0008, 1.25F },
        { "Device name", 0x0009, "Device name" },
        { "Grüße", 0x000B, "Grüße" },
        { "-8388608", 0x0010, -8388608 },
        { "1.25", 0x0011, 1.25D },
        { "-549755813888", 0x0012, -549755813888L },
        { "9223372036854775807", 0x0015, long.MaxValue },
        { "16777215", 0x0016, 16777215U },
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
