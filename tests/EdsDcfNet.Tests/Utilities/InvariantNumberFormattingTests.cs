using System.Globalization;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Extensions;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

namespace EdsDcfNet.Tests.Utilities;

/// <summary>
/// Numbers embedded in file output and diagnostic messages must not depend on the thread's
/// <see cref="CultureInfo.CurrentCulture"/>. The culture under test uses a decimal comma and a
/// non-ASCII minus sign so any culture-sensitive formatting becomes visible.
/// </summary>
public class InvariantNumberFormattingTests
{
    private const string UnicodeMinus = "−";

    public static TheoryData<string> CultureNames => new() { "de-DE", "ar-SA", "sv-SE" };

    private static CultureInfo CreateHostileCulture(string name)
    {
        var culture = new CultureInfo(name);
        culture.NumberFormat.NegativeSign = UnicodeMinus;
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.PositiveInfinitySymbol = "unendlich";
        culture.NumberFormat.NaNSymbol = "keine Zahl";
        return culture;
    }

    private static void WithCulture(string name, Action action)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CreateHostileCulture(name);
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static ElectronicDataSheet CreateEds() => new()
    {
        FileInfo = new EdsFileInfo { FileName = "test.eds", FileVersion = 1, EdsVersion = "4.0" },
        DeviceInfo = new DeviceInfo { VendorName = "V", ProductName = "Product" },
        ObjectDictionary = new ObjectDictionary()
    };

    [Theory]
    [MemberData(nameof(CultureNames))]
    public void ConvertToDcf_NonInvariantCurrentCulture_NodeNameUsesInvariantNodeId(string cultureName)
    {
        // Arrange
        var eds = CreateEds();
        DeviceConfigurationFile? dcf = null;

        // Act
        WithCulture(cultureName, () =>
            dcf = CanOpenFile.Eds.ConvertToDcf(eds, 12, new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc)));

        // Assert
        dcf!.DeviceCommissioning.NodeName.Should().Be("Product_Node12");
    }

    [Theory]
    [MemberData(nameof(CultureNames))]
    public void FormatInteger_NonInvariantCurrentCulture_ProducesInvariantOutput(string cultureName)
    {
        // Arrange
        string? hex = null;
        string? dec = null;

        // Act
        WithCulture(cultureName, () =>
        {
            hex = ValueConverter.FormatInteger(0xFFFFFFFFu);
            dec = ValueConverter.FormatInteger(1234567u, useHex: false);
        });

        // Assert
        hex.Should().Be("0xFFFFFFFF");
        dec.Should().Be("1234567");
    }

    [Theory]
    [MemberData(nameof(CultureNames))]
    public void CanOpenValueConverterFormat_NegativeOutOfRange_MessageUsesInvariantMinusSign(string cultureName)
    {
        // Arrange
        Exception? caught = null;

        // Act
        WithCulture(cultureName, () =>
            caught = Record.Exception(() => CanOpenValueConverter.Format(-200, CanOpenDataType.Integer8)));

        // Assert
        caught.Should().BeOfType<ArgumentException>();
        caught!.Message.Should().Contain("'-200'");
        caught.InnerException!.Message.Should().Be("Value -200 is outside the signed 8-bit range.");
    }

    [Theory]
    [MemberData(nameof(CultureNames))]
    public void CanOpenValueConverterFormat_UnsignedOutOfRange_MessageUsesInvariantDigits(string cultureName)
    {
        // Arrange
        Exception? caught = null;

        // Act
        WithCulture(cultureName, () =>
            caught = Record.Exception(() => CanOpenValueConverter.Format(300UL, CanOpenDataType.Unsigned8)));

        // Assert
        caught!.InnerException!.Message.Should().Be("Value 300 is outside the unsigned 8-bit range.");
    }

    [Theory]
    [MemberData(nameof(CultureNames))]
    public void CanOpenValueConverterFormat_FractionalValueForInteger_MessageUsesInvariantDecimalSeparator(string cultureName)
    {
        // Arrange
        Exception? caught = null;

        // Act
        WithCulture(cultureName, () =>
            caught = Record.Exception(() => CanOpenValueConverter.Format(1.5, CanOpenDataType.Integer32)));

        // Assert
        caught.Should().BeOfType<ArgumentException>();
        caught!.Message.Should().Contain("'1.5'");
        caught.InnerException!.Message.Should().Contain("'1.5'");
    }

    [Theory]
    [MemberData(nameof(CultureNames))]
    public void CanOpenValueConverterFormat_InfiniteReal_MessageUsesInvariantInfinitySymbol(string cultureName)
    {
        // Arrange
        Exception? caught = null;

        // Act
        WithCulture(cultureName, () =>
            caught = Record.Exception(() => CanOpenValueConverter.Format(double.PositiveInfinity, CanOpenDataType.Real32)));

        // Assert
        caught!.InnerException!.Message.Should().Be("Value 'Infinity' is outside the finite REAL32 range.");
    }

    [Theory]
    [MemberData(nameof(CultureNames))]
    public void CanOpenValueConverterFormat_NonFiniteNaN_MessageUsesInvariantNaNSymbol(string cultureName)
    {
        // Arrange
        Exception? caught = null;

        // Act
        WithCulture(cultureName, () =>
            caught = Record.Exception(() => CanOpenValueConverter.Format(double.NaN, CanOpenDataType.Real64)));

        // Assert
        caught!.InnerException!.Message.Should().Be("'NaN' is not a valid CANopen REAL64 value.");
    }

    [Theory]
    [MemberData(nameof(CultureNames))]
    public void CanOpenValueConverterFormat_BooleanOutOfRange_MessageUsesInvariantMinusSign(string cultureName)
    {
        // Arrange
        Exception? caught = null;

        // Act
        WithCulture(cultureName, () =>
            caught = Record.Exception(() => CanOpenValueConverter.Format(-5, CanOpenDataType.Boolean)));

        // Assert
        caught!.InnerException!.Message.Should().Be("Value -5 is outside the CANopen BOOLEAN value range (0 or 1).");
    }

    [Theory]
    [MemberData(nameof(CultureNames))]
    public void CanOpenValueConverterFormat_UnsupportedDataType_MessageUsesInvariantHex(string cultureName)
    {
        // Arrange
        Exception? caught = null;

        // Act
        WithCulture(cultureName, () =>
            caught = Record.Exception(() => CanOpenValueConverter.Format(1, 0x00FF)));

        // Assert
        caught.Should().BeOfType<NotSupportedException>();
        caught!.Message.Should().Be("CANopen data type 0x00FF is not supported for typed value conversion.");
    }

    [Theory]
    [MemberData(nameof(CultureNames))]
    public void ParseDiagnosticToString_NonInvariantCurrentCulture_UsesInvariantLineNumber(string cultureName)
    {
        // Arrange
        var withPath = new ParseDiagnostic(ParseSeverity.Warning, "X", "Sec/Key", "msg", line: 1234567);
        var withoutPath = new ParseDiagnostic(ParseSeverity.Warning, "X", string.Empty, "msg", line: 1234567);
        string? a = null;
        string? b = null;

        // Act
        WithCulture(cultureName, () =>
        {
            a = withPath.ToString();
            b = withoutPath.ToString();
        });

        // Assert
        a.Should().Be("[Warning] X at Sec/Key:1234567: msg");
        b.Should().Be("[Warning] X at line 1234567: msg");
    }

    [Theory]
    [MemberData(nameof(CultureNames))]
    public void EdsReadString_KeyValueOutsideSection_MessageUsesInvariantLineNumber(string cultureName)
    {
        // Arrange
        Exception? caught = null;

        // Act
        WithCulture(cultureName, () =>
            caught = Record.Exception(() => CanOpenFile.Eds.ReadString("Key=Value\n")));

        // Assert
        caught.Should().BeOfType<EdsParseException>();
        caught!.Message.Should().Contain("at line 1");
    }

    [Theory]
    [MemberData(nameof(CultureNames))]
    public void ObjectDictionaryTypedValue_MissingDataType_MessageUsesInvariantHex(string cultureName)
    {
        // Arrange
        var od = new ObjectDictionary();
        od.Objects[0x1A2B] = new CanOpenObject { Index = 0x1A2B, ParameterValue = "5", DataType = 0 };
        Exception? caught = null;

        // Act
        WithCulture(cultureName, () =>
            caught = Record.Exception(() => od.GetParameterValueAsObject(0x1A2B)));

        // Assert
        caught.Should().BeOfType<InvalidOperationException>();
        caught!.Message.Should().Be("Object 0x1A2B does not define a CANopen data type.");
    }

    [Theory]
    [MemberData(nameof(CultureNames))]
    public void ObjectDictionaryTypedValue_WrongTypeForSubObject_MessageUsesInvariantHexAddress(string cultureName)
    {
        // Arrange
        var od = new ObjectDictionary();
        var obj = new CanOpenObject { Index = 0x1A2B, ParameterValue = "5", DataType = CanOpenDataType.Unsigned8 };
        od.Objects[0x1A2B] = obj;
        Exception? caught = null;

        // Act
        WithCulture(cultureName, () =>
            caught = Record.Exception(() => od.GetParameterValue<string>(0x1A2B)));

        // Assert
        caught.Should().BeOfType<InvalidCastException>();
        caught!.Message.Should().StartWith("Object Dictionary value 0x1A2B has .NET type");
    }
}
