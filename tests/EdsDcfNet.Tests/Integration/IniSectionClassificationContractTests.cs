namespace EdsDcfNet.Tests.Integration;

using EdsDcfNet.Parsers;

/// <summary>
/// Every production consumer of module-section recognition must use
/// <see cref="CanOpenSectionParsers.TryClassifyModuleSection"/>. Fixed-object
/// bodies have separate consumed/orphan handling; the other recognized sections
/// are known to <c>CanOpenReaderBase.IsKnownSection</c>. Unknown vendor names are
/// preserved, including in strict mode.
/// </summary>
public class IniSectionClassificationContractTests
{
    [Theory]
    [InlineData("M1ModuleInfo", true)]
    [InlineData("M1FixedObjects", true)]
    [InlineData("M1SubExtends", true)]
    [InlineData("M1Comments", true)]
    [InlineData("M1SubExt2000", true)]
    [InlineData("M01SubExt2000", true)]
    [InlineData("M1Fixed6000", true)]
    [InlineData("M1Fixed6000sub1", true)]
    [InlineData("M1ModuleInfoVendor", false)]
    [InlineData("M01ModuleInfo", false)]
    [InlineData("M1FixedObjectsVendor", false)]
    [InlineData("M1SubExtGarbage", false)]
    [InlineData("M1Fixed6000Garbage", false)]
    [InlineData("M1Fixed10000", false)]
    [InlineData("M1Fixed1sub100", false)]
    [InlineData("M1SubExt10000", false)]
    [InlineData("M2147483648ModuleInfo", false)]
    [InlineData("M999999999999999999999Comments", false)]
    [InlineData("MComments", false)]
    public void TryClassifyModuleSection_AdversarialName_MatchesParserGrammar(string sectionName, bool expected)
    {
        // Act
        var recognized = CanOpenSectionParsers.TryClassifyModuleSection(sectionName, out _, out _);

        // Assert
        recognized.Should().Be(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadString_UnrecognizedVendorModuleNames_ArePreservedInBothModes(bool strict)
    {
        // Arrange — none of these names is loaded by the module parser. A prefix-only
        // recognizer would hide them as known sections and change strict behavior.
        const string content = """
            [DeviceInfo]
            VendorName=Test
            ProductName=Coupler

            [MandatoryObjects]
            SupportedObjects=0

            [SupportedModules]
            NrOfEntries=0

            [M1ModuleInfoVendor]
            Value=one

            [M01ModuleInfo]
            Value=two

            [M1SubExtGarbage]
            Value=three

            [M999999999999999999999Comments]
            Value=four
            """;
        var options = strict ? new CanOpenFileOptions { StrictParsing = true } : null;

        // Act
        var eds = CanOpenFile.Eds.ReadString(content, options);
        var written = CanOpenFile.Eds.WriteToString(eds);

        // Assert
        eds.AdditionalSections.Keys.Should().BeEquivalentTo(
            "M1ModuleInfoVendor",
            "M01ModuleInfo",
            "M1SubExtGarbage",
            "M999999999999999999999Comments");
        written.Should().Contain("Value=one")
            .And.Contain("Value=two")
            .And.Contain("Value=three")
            .And.Contain("Value=four");
    }
}
