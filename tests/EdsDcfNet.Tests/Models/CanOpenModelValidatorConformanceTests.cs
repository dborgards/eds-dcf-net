namespace EdsDcfNet.Tests.Models;

using EdsDcfNet.Models;
using EdsDcfNet.Validation;

/// <summary>
/// CiA 306 conformance checks added for #562: SubNumber count, values vs. data type,
/// limit consistency and the opt-in mandatory-entry rule set.
/// </summary>
public class CanOpenModelValidatorConformanceTests
{
    private static ElectronicDataSheet EdsWithVar(ushort dataType, string? defaultValue, string? low = null, string? high = null)
    {
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.ManufacturerObjects.Add(0x2000);
        eds.ObjectDictionary.Objects[0x2000] = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Value",
            ObjectType = CanOpenObjectType.Var,
            DataType = dataType,
            AccessType = AccessType.ReadWrite,
            DefaultValue = defaultValue,
            LowLimit = low,
            HighLimit = high,
        };
        return eds;
    }

    private static ElectronicDataSheet EdsWithArray(byte? subNumber, params byte[] subIndexes)
    {
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.ManufacturerObjects.Add(0x2100);
        var obj = new CanOpenObject
        {
            Index = 0x2100,
            ParameterName = "List",
            ObjectType = CanOpenObjectType.Array,
            SubNumber = subNumber,
        };
        foreach (var sub in subIndexes)
        {
            obj.SubObjects[sub] = new CanOpenSubObject
            {
                SubIndex = sub,
                ParameterName = "Sub",
                DataType = CanOpenDataType.Unsigned8,
                DefaultValue = "1",
            };
        }

        eds.ObjectDictionary.Objects[0x2100] = obj;
        return eds;
    }

    // ------------------------------------------------------------------ SubNumber

    [Fact]
    public void Validate_SubNumberCountsSubIndexZero_ReturnsNoIssues()
    {
        CanOpenModelValidator.Validate(EdsWithArray(3, 0, 1, 4)).Should().BeEmpty();
    }

    [Fact]
    public void Validate_SubNumberExcludesSubIndexZero_ReportsIssue()
    {
        var issues = CanOpenModelValidator.Validate(EdsWithArray(4, 0, 1, 2, 3, 4));

        issues.Should().ContainSingle(i => i.Path == "ObjectDictionary.Objects[0x2100].SubNumber")
            .Which.Message.Should().Contain("SubNumber is 4 but 5 sub-objects");
    }

    [Fact]
    public void Validate_SubNumberZeroWithOnlySubIndexZero_KeepsExistingTolerance()
    {
        CanOpenModelValidator.Validate(EdsWithArray(0, 0)).Should().BeEmpty();
    }

    [Fact]
    public void Validate_CompactSubObjWithDifferentSubNumber_IsNotCounted()
    {
        var eds = EdsWithArray(16, 0);
        eds.ObjectDictionary.Objects[0x2100].CompactSubObj = 16;

        CanOpenModelValidator.Validate(eds).Should()
            .NotContain(i => i.Path == "ObjectDictionary.Objects[0x2100].SubNumber");
    }

    // ------------------------------------------------------------------ values vs. data type

    [Theory]
    [InlineData(CanOpenDataType.Unsigned8, "255")]
    [InlineData(CanOpenDataType.Unsigned8, "0xFF")]
    [InlineData(CanOpenDataType.Integer16, "32767")]
    [InlineData(CanOpenDataType.Integer16, "-32768")]
    [InlineData(CanOpenDataType.Unsigned64, "0xFFFFFFFFFFFFFFFF")]
    [InlineData(CanOpenDataType.Integer64, "-9223372036854775808")]
    [InlineData(CanOpenDataType.Boolean, "0x1")]
    [InlineData(CanOpenDataType.Real32, "1.5")]
    [InlineData(CanOpenDataType.Real32, "0x3F800000")] // hex bit pattern written by some tools: not checked
    [InlineData(CanOpenDataType.Real64, "0x3FF0000000000000")]
    public void Validate_DefaultValue_AtMaxValue_ReturnsNoIssues(ushort dataType, string value)
    {
        CanOpenModelValidator.Validate(EdsWithVar(dataType, value)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(CanOpenDataType.Unsigned8, "256", "UNSIGNED8 (0..255)")]
    [InlineData(CanOpenDataType.Unsigned8, "1000", "UNSIGNED8 (0..255)")]
    [InlineData(CanOpenDataType.Integer16, "32768", "INTEGER16 (-32768..32767)")]
    [InlineData(CanOpenDataType.Integer8, "150", "INTEGER8 (-128..127)")]
    [InlineData(CanOpenDataType.Unsigned16, "08", "UNSIGNED16")]
    [InlineData(CanOpenDataType.Boolean, "2", "BOOLEAN")]
    [InlineData(CanOpenDataType.Real32, "abc", "REAL32")]
    public void Validate_DefaultValueOutsideDataType_ReportsIssue(ushort dataType, string value, string expectedType)
    {
        var issues = CanOpenModelValidator.Validate(EdsWithVar(dataType, value));

        issues.Should().ContainSingle(i => i.Path == "ObjectDictionary.Objects[0x2000].DefaultValue")
            .Which.Message.Should().Contain(expectedType);
    }

    [Fact]
    public void Validate_HighLimitOutsideDataType_ReportsIssue()
    {
        var issues = CanOpenModelValidator.Validate(EdsWithVar(CanOpenDataType.Unsigned8, "0", "0", "1000"));

        issues.Should().ContainSingle(i => i.Path == "ObjectDictionary.Objects[0x2000].HighLimit");
    }

    [Theory]
    [InlineData(CanOpenDataType.Domain, "anything")]
    [InlineData(CanOpenDataType.VisibleString, "text")]
    public void Validate_NonComparableDataType_SkipsValueCheck(ushort dataType, string value)
    {
        CanOpenModelValidator.Validate(EdsWithVar(dataType, value, "x", "y")).Should().BeEmpty();
    }

    [Fact]
    public void Validate_LowLimitGreaterThanHighLimit_ReportsIssue()
    {
        var issues = CanOpenModelValidator.Validate(EdsWithVar(CanOpenDataType.Unsigned16, null, "500", "100"));

        issues.Should().ContainSingle(i => i.Path == "ObjectDictionary.Objects[0x2000].LowLimit")
            .Which.Message.Should().Be("LowLimit 500 is greater than HighLimit 100.");
    }

    [Theory]
    [InlineData("20", "above HighLimit 10")]
    [InlineData("-11", "below LowLimit -10")]
    public void Validate_DefaultValueOutsideLimits_ReportsIssue(string value, string expected)
    {
        var issues = CanOpenModelValidator.Validate(EdsWithVar(CanOpenDataType.Integer16, value, "-10", "10"));

        issues.Should().ContainSingle(i => i.Path == "ObjectDictionary.Objects[0x2000].DefaultValue")
            .Which.Message.Should().Contain(expected);
    }

    [Theory]
    [InlineData("10")]
    [InlineData("-10")]
    public void Validate_DefaultValueOnLimit_AtMaxValue_ReturnsNoIssues(string value)
    {
        CanOpenModelValidator.Validate(EdsWithVar(CanOpenDataType.Integer16, value, "-10", "10")).Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyLowLimit_IsNotTreatedAsZero()
    {
        CanOpenModelValidator.Validate(EdsWithVar(CanOpenDataType.Integer16, "-5", "", "10")).Should().BeEmpty();
    }

    [Fact]
    public void Validate_RealLimits_ComparesAsFloatingPoint()
    {
        var issues = CanOpenModelValidator.Validate(EdsWithVar(CanOpenDataType.Real64, "1.5", "0.5", "1.25"));

        issues.Should().ContainSingle(i => i.Path == "ObjectDictionary.Objects[0x2000].DefaultValue");
    }

    [Fact]
    public void Validate_EdsNodeIdFormula_IsCheckedForHighestNodeId()
    {
        // 127 + 0x81 = 256 does not fit UNSIGNED8, while node-ID 1 would.
        var issues = CanOpenModelValidator.Validate(EdsWithVar(CanOpenDataType.Unsigned8, "$NODEID+0x81"));

        issues.Should().ContainSingle(i => i.Path == "ObjectDictionary.Objects[0x2000].DefaultValue")
            .Which.Message.Should().Contain("for node-ID 127");
    }

    [Fact]
    public void Validate_DcfNodeIdFormula_UsesConfiguredNodeId()
    {
        var dcf = new DeviceConfigurationFile();
        dcf.DeviceCommissioning.NodeId = 1;
        dcf.DeviceCommissioning.Baudrate = 250;
        dcf.ObjectDictionary.ManufacturerObjects.Add(0x2000);
        dcf.ObjectDictionary.Objects[0x2000] = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "COB-ID",
            DataType = CanOpenDataType.Unsigned8,
            DefaultValue = "$NODEID+0x81",
        };

        CanOpenModelValidator.Validate(dcf).Should().BeEmpty();
    }

    [Fact]
    public void Validate_DcfParameterValueOutsideLimits_ReportsIssue()
    {
        var dcf = new DeviceConfigurationFile();
        dcf.ObjectDictionary.ManufacturerObjects.Add(0x2000);
        dcf.ObjectDictionary.Objects[0x2000] = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Value",
            DataType = CanOpenDataType.Unsigned16,
            LowLimit = "0",
            HighLimit = "100",
            DefaultValue = "50",
            ParameterValue = "200",
        };

        var issues = CanOpenModelValidator.Validate(dcf);

        issues.Should().ContainSingle(i => i.Path == "ObjectDictionary.Objects[0x2000].ParameterValue")
            .Which.Message.Should().Contain("above HighLimit 100");
    }

    [Fact]
    public void Validate_SubObjectValueOutsideDataType_ReportsIssueWithSubPath()
    {
        var eds = EdsWithArray(2, 0, 1);
        eds.ObjectDictionary.Objects[0x2100].SubObjects[1].DefaultValue = "300";

        var issues = CanOpenModelValidator.Validate(eds);

        issues.Should().ContainSingle(i => i.Path == "ObjectDictionary.Objects[0x2100].SubObjects[0x01].DefaultValue");
    }

    [Fact]
    public void Validate_DefStructMembers_AreNotValueChecked()
    {
        var eds = EdsWithArray(2, 0, 1);
        var obj = eds.ObjectDictionary.Objects[0x2100];
        obj.ObjectType = CanOpenObjectType.DefStruct;
        obj.SubObjects[1].DefaultValue = "0x0707"; // member type/length encoding, not a value

        CanOpenModelValidator.Validate(eds).Should().BeEmpty();
    }

    // ------------------------------------------------------------------ opt-in mandatory entries

    [Fact]
    public void Validate_EmptyEdsWithDefaultOptions_StaysClean()
    {
        CanOpenModelValidator.Validate(new ElectronicDataSheet()).Should().BeEmpty();
        CanOpenModelValidator.Validate(new ElectronicDataSheet(), null).Should().BeEmpty();
        CanOpenModelValidator.Validate(new ElectronicDataSheet(), CanOpenValidationOptions.Default).Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyEdsWithStrictOptions_ReportsMandatoryEntries()
    {
        var issues = CanOpenModelValidator.Validate(new ElectronicDataSheet(), CanOpenValidationOptions.Strict);

        issues.Select(i => i.Path).Should().BeEquivalentTo(
            "FileInfo.FileName",
            "DeviceInfo.VendorName",
            "DeviceInfo.ProductName",
            "ObjectDictionary.Objects[0x1000]",
            "ObjectDictionary.Objects[0x1001]",
            "ObjectDictionary.Objects[0x1018]");
    }

    [Fact]
    public void Validate_StrictOptions_ReportsVarWithoutDataTypeAndEmptyParameterNames()
    {
        var eds = EdsWithVar(CanOpenDataType.Unsigned8, "1");
        var obj = eds.ObjectDictionary.Objects[0x2000];
        obj.DataType = null;
        obj.ParameterName = " ";
        var array = EdsWithArray(2, 0, 1).ObjectDictionary.Objects[0x2100];
        array.SubObjects[1].ParameterName = string.Empty;
        array.SubObjects[1].DataType = 0;
        eds.ObjectDictionary.Objects[0x2100] = array;

        var issues = CanOpenModelValidator.Validate(eds, CanOpenValidationOptions.Strict);

        issues.Select(i => i.Path).Should().Contain(new[]
        {
            "ObjectDictionary.Objects[0x2000].DataType",
            "ObjectDictionary.Objects[0x2000].ParameterName",
            "ObjectDictionary.Objects[0x2100].SubObjects[0x01].ParameterName",
            "ObjectDictionary.Objects[0x2100].SubObjects[0x01].DataType",
        });
    }

    [Fact]
    public void Validate_StrictOptions_DcfWithoutCommissioning_ReportsIssue()
    {
        var issues = CanOpenModelValidator.Validate(new DeviceConfigurationFile(), CanOpenValidationOptions.Strict);

        issues.Should().Contain(i => i.Path == "DeviceCommissioning");
    }

    [Fact]
    public void Validate_StrictOptions_ValidDcf_ReturnsNoIssues()
    {
        var dcf = CanOpenFile.Dcf.ReadFile("Fixtures/full_features.dcf");

        CanOpenModelValidator.Validate(dcf, CanOpenValidationOptions.Strict).Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_WithOptions_MatchesSynchronousResult()
    {
        var eds = new ElectronicDataSheet();

        var asyncIssues = await CanOpenModelValidator.ValidateAsync(eds, CanOpenValidationOptions.Strict, CancellationToken.None);
        var dcfIssues = await CanOpenModelValidator.ValidateAsync(new DeviceConfigurationFile(), null, CancellationToken.None);

        asyncIssues.Select(i => i.Path).Should().BeEquivalentTo(
            CanOpenModelValidator.Validate(eds, CanOpenValidationOptions.Strict).Select(i => i.Path));
        dcfIssues.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_DefaultLiteralToken_StillBindsToOverloadWithoutOptions()
    {
        // Source compatibility: the options overload has no defaulted token, so this call is not ambiguous.
        var issues = await CanOpenModelValidator.ValidateAsync(new ElectronicDataSheet(), default);

        issues.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithOptions_NullModel_Throws()
    {
        var act = () => CanOpenModelValidator.Validate((ElectronicDataSheet)null!, CanOpenValidationOptions.Strict);
        var actDcf = () => CanOpenModelValidator.Validate((DeviceConfigurationFile)null!, null);

        act.Should().Throw<ArgumentNullException>();
        actDcf.Should().Throw<ArgumentNullException>();
    }
}
