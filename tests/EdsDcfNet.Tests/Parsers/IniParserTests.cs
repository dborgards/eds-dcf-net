namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Parsers;
using FluentAssertions;
using Xunit;

public class IniParserTests
{
    #region ParseString Tests

    [Fact]
    public void ParseString_SimpleSectionWithKeyValue_ParsesCorrectly()
    {
        // Arrange
        var content = @"
[Section1]
Key1=Value1
Key2=Value2
";
        // Act
        var result = IniParser.ParseString(content);

        // Assert
        result.Should().ContainKey("Section1");
        result["Section1"].Should().ContainKey("Key1").WhoseValue.Should().Be("Value1");
        result["Section1"].Should().ContainKey("Key2").WhoseValue.Should().Be("Value2");
    }

    [Fact]
    public void ParseString_MultipleSections_ParsesCorrectly()
    {
        // Arrange
        var content = @"
[Section1]
Key1=Value1

[Section2]
Key2=Value2
Key3=Value3
";
        // Act
        var result = IniParser.ParseString(content);

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainKey("Section1");
        result.Should().ContainKey("Section2");
        result["Section1"]["Key1"].Should().Be("Value1");
        result["Section2"]["Key2"].Should().Be("Value2");
        result["Section2"]["Key3"].Should().Be("Value3");
    }

    [Fact]
    public void ParseString_CommentsAreIgnored()
    {
        // Arrange
        var content = @"
; This is a comment
[Section1]
; Another comment
Key1=Value1
; Comment in the middle
Key2=Value2
";
        // Act
        var result = IniParser.ParseString(content);

        // Assert
        result.Should().ContainKey("Section1");
        result["Section1"].Should().HaveCount(2);
        result["Section1"]["Key1"].Should().Be("Value1");
        result["Section1"]["Key2"].Should().Be("Value2");
    }

    [Fact]
    public void ParseString_EmptyLinesAreIgnored()
    {
        // Arrange
        var content = @"


[Section1]

Key1=Value1


Key2=Value2

";
        // Act
        var result = IniParser.ParseString(content);

        // Assert
        result.Should().ContainKey("Section1");
        result["Section1"].Should().HaveCount(2);
    }

    [Fact]
    public void ParseString_WhitespaceIsTrimmed()
    {
        // Arrange
        var content = @"
[  Section1  ]
  Key1  =  Value1
Key2=Value2
";
        // Act
        var result = IniParser.ParseString(content);

        // Assert
        result.Should().ContainKey("Section1");
        result["Section1"]["Key1"].Should().Be("Value1");
        result["Section1"]["Key2"].Should().Be("Value2");
    }

    [Fact]
    public void ParseString_SectionNameIsCaseInsensitive()
    {
        // Arrange
        var content = @"
[Section1]
Key1=Value1
";
        // Act
        var result = IniParser.ParseString(content);

        // Assert
        result.Should().ContainKey("section1");
        result.Should().ContainKey("SECTION1");
        result.Should().ContainKey("Section1");
    }

    [Fact]
    public void ParseString_KeyNameIsCaseInsensitive()
    {
        // Arrange
        var content = @"
[Section1]
Key1=Value1
";
        // Act
        var result = IniParser.ParseString(content);

        // Assert
        result["Section1"].Should().ContainKey("key1");
        result["Section1"].Should().ContainKey("KEY1");
        result["Section1"].Should().ContainKey("Key1");
    }

    [Fact]
    public void ParseString_EmptyValue_ParsesAsEmptyString()
    {
        // Arrange
        var content = @"
[Section1]
Key1=
Key2=Value2
";
        // Act
        var result = IniParser.ParseString(content);

        // Assert
        result["Section1"]["Key1"].Should().Be(string.Empty);
        result["Section1"]["Key2"].Should().Be("Value2");
    }

    [Fact]
    public void ParseString_ValueWithEqualSign_ParsesCorrectly()
    {
        // Arrange
        var content = @"
[Section1]
Key1=Value=With=Equals
";
        // Act
        var result = IniParser.ParseString(content);

        // Assert
        result["Section1"]["Key1"].Should().Be("Value=With=Equals");
    }

    [Fact]
    public void ParseString_HexadecimalValues_PreservesFormat()
    {
        // Arrange
        var content = @"
[Section1]
Key1=0xFF
Key2=0x1000
Key3=$NODEID+0x200
";
        // Act
        var result = IniParser.ParseString(content);

        // Assert
        result["Section1"]["Key1"].Should().Be("0xFF");
        result["Section1"]["Key2"].Should().Be("0x1000");
        result["Section1"]["Key3"].Should().Be("$NODEID+0x200");
    }

    [Fact]
    public void ParseString_KeyValueOutsideSection_ThrowsException()
    {
        // Arrange
        var content = @"
Key1=Value1
[Section1]
Key2=Value2
";
        // Act
        var act = () => IniParser.ParseString(content);

        // Assert
        act.Should().Throw<EdsParseException>()
            .WithMessage("*Key-value pair found outside of any section*");
    }

    [Fact]
    public void ParseString_DuplicateSection_MergesKeys()
    {
        // Arrange
        var content = @"
[Section1]
Key1=Value1

[Section1]
Key2=Value2
";
        // Act
        var result = IniParser.ParseString(content);

        // Assert
        result.Should().ContainKey("Section1");
        result["Section1"].Should().HaveCount(2);
        result["Section1"]["Key1"].Should().Be("Value1");
        result["Section1"]["Key2"].Should().Be("Value2");
    }

    [Fact]
    public void ParseString_DuplicateKey_UsesLastValue()
    {
        // Arrange
        var content = @"
[Section1]
Key1=Value1
Key1=Value2
";
        // Act
        var result = IniParser.ParseString(content);

        // Assert
        result["Section1"]["Key1"].Should().Be("Value2");
    }

    [Fact]
    public void ParseString_SubObjectSyntax_ParsesCorrectly()
    {
        // Arrange
        var content = @"
[1018sub0]
ParameterName=Number of Entries
DataType=0x0005
";
        // Act
        var result = IniParser.ParseString(content);

        // Assert
        result.Should().ContainKey("1018sub0");
        result["1018sub0"]["ParameterName"].Should().Be("Number of Entries");
        result["1018sub0"]["DataType"].Should().Be("0x0005");
    }

    [Fact]
    public void ParseString_EmptyInput_ReturnsEmptyDictionary()
    {
        // Arrange
        var content = string.Empty;
        // Act
        var result = IniParser.ParseString(content);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseString_OnlyComments_ReturnsEmptyDictionary()
    {
        // Arrange
        var content = @"
; Comment 1
; Comment 2
; Comment 3
";
        // Act
        var result = IniParser.ParseString(content);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseString_ContentTooLarge_ThrowsEdsParseException()
    {
        // Arrange
        var content = "[Section1]\nKey1=Value1"; // 22 chars > 10

        // Act
        var act = () => IniParser.ParseString(content, maxInputSize: 10);

        // Assert
        act.Should().Throw<EdsParseException>()
            .WithMessage("*too large*");
    }

    [Fact]
    public void ParseString_VeryLongLineValue_ParsesCorrectly()
    {
        // Arrange – a value of 10 000 characters; no line-length limit is defined
        var longValue = new string('X', 10_000);
        var content = $"[Section1]\nKey1={longValue}\n";

        // Act
        var result = IniParser.ParseString(content);

        // Assert
        result["Section1"]["Key1"].Should().Be(longValue);
    }

    [Fact]
    public void ParseString_ManySections_AllParsedCorrectly()
    {
        // Arrange – 500 sections; the parser must not degrade or lose data
        var sb = new System.Text.StringBuilder();
        const int count = 500;
        for (int i = 0; i < count; i++)
        {
            sb.AppendLine($"[Sec{i}]");
            sb.AppendLine($"Key=Value{i}");
        }

        // Act
        var result = IniParser.ParseString(sb.ToString());

        // Assert
        result.Should().HaveCount(count);
        result["Sec0"]["Key"].Should().Be("Value0");
        result["Sec499"]["Key"].Should().Be("Value499");
    }

    #endregion

    #region ParseFile Tests

    [Fact]
    public void ParseFile_ValidFile_ParsesCorrectly()
    {
        // Arrange
        var filePath = "Fixtures/sample_device.eds";

        // Act
        var result = IniParser.ParseFile(filePath);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().ContainKey("FileInfo");
        result.Should().ContainKey("DeviceInfo");
        result.Should().ContainKey("1000");
    }

    [Fact]
    public void ParseFile_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var filePath = "NonExistent.eds";

        // Act
        var act = () => IniParser.ParseFile(filePath);

        // Assert
        act.Should().Throw<FileNotFoundException>()
            .WithMessage("*EDS/DCF file not found*");
    }

    [Fact]
    public void ParseFile_FileTooLarge_ThrowsEdsParseException()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(tempFile, "[Section1]\nKey1=Value1"); // 22 bytes > 10

            // Act
            var act = () => IniParser.ParseFile(tempFile, maxInputSize: 10);

            // Assert
            act.Should().Throw<EdsParseException>()
                .WithMessage("*too large*");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region GetValue Tests

    [Fact]
    public void GetValue_ExistingKey_ReturnsValue()
    {
        // Arrange
        var sections = new Dictionary<string, Dictionary<string, string>>
        {
            ["Section1"] = new Dictionary<string, string>
            {
                ["Key1"] = "Value1"
            }
        };

        // Act
        var result = IniParser.GetValue(sections, "Section1", "Key1");

        // Assert
        result.Should().Be("Value1");
    }

    [Fact]
    public void GetValue_NonExistentKey_ReturnsDefaultValue()
    {
        // Arrange
        var sections = new Dictionary<string, Dictionary<string, string>>
        {
            ["Section1"] = new Dictionary<string, string>
            {
                ["Key1"] = "Value1"
            }
        };

        // Act
        var result = IniParser.GetValue(sections, "Section1", "NonExistentKey", "Default");

        // Assert
        result.Should().Be("Default");
    }

    [Fact]
    public void GetValue_NonExistentSection_ReturnsDefaultValue()
    {
        // Arrange
        var sections = new Dictionary<string, Dictionary<string, string>>
        {
            ["Section1"] = new Dictionary<string, string>
            {
                ["Key1"] = "Value1"
            }
        };

        // Act
        var result = IniParser.GetValue(sections, "NonExistentSection", "Key1", "Default");

        // Assert
        result.Should().Be("Default");
    }

    [Fact]
    public void GetValue_NoDefaultValue_ReturnsEmptyString()
    {
        // Arrange
        var sections = new Dictionary<string, Dictionary<string, string>>
        {
            ["Section1"] = new Dictionary<string, string>()
        };

        // Act
        var result = IniParser.GetValue(sections, "Section1", "NonExistentKey");

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void GetValue_CaseInsensitive_ReturnsValue()
    {
        // Arrange
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Section1"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Key1"] = "Value1"
            }
        };

        // Act
        var result1 = IniParser.GetValue(sections, "section1", "key1");
        var result2 = IniParser.GetValue(sections, "SECTION1", "KEY1");

        // Assert
        result1.Should().Be("Value1");
        result2.Should().Be("Value1");
    }

    #endregion

    #region HasSection Tests

    [Fact]
    public void HasSection_ExistingSection_ReturnsTrue()
    {
        // Arrange
        var sections = new Dictionary<string, Dictionary<string, string>>
        {
            ["Section1"] = new Dictionary<string, string>()
        };

        // Act
        var result = IniParser.HasSection(sections, "Section1");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasSection_NonExistentSection_ReturnsFalse()
    {
        // Arrange
        var sections = new Dictionary<string, Dictionary<string, string>>
        {
            ["Section1"] = new Dictionary<string, string>()
        };

        // Act
        var result = IniParser.HasSection(sections, "NonExistentSection");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasSection_EmptySections_ReturnsFalse()
    {
        // Arrange
        var sections = new Dictionary<string, Dictionary<string, string>>();

        // Act
        var result = IniParser.HasSection(sections, "Section1");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetKeys Tests

    [Fact]
    public void GetKeys_ExistingSection_ReturnsAllKeys()
    {
        // Arrange
        var sections = new Dictionary<string, Dictionary<string, string>>
        {
            ["Section1"] = new Dictionary<string, string>
            {
                ["Key1"] = "Value1",
                ["Key2"] = "Value2",
                ["Key3"] = "Value3"
            }
        };

        // Act
        var result = IniParser.GetKeys(sections, "Section1");

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("Key1");
        result.Should().Contain("Key2");
        result.Should().Contain("Key3");
    }

    [Fact]
    public void GetKeys_NonExistentSection_ReturnsEmptyEnumerable()
    {
        // Arrange
        var sections = new Dictionary<string, Dictionary<string, string>>
        {
            ["Section1"] = new Dictionary<string, string>()
        };

        // Act
        var result = IniParser.GetKeys(sections, "NonExistentSection");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetKeys_EmptySection_ReturnsEmptyEnumerable()
    {
        // Arrange
        var sections = new Dictionary<string, Dictionary<string, string>>
        {
            ["Section1"] = new Dictionary<string, string>()
        };

        // Act
        var result = IniParser.GetKeys(sections, "Section1");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion
}
