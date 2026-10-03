namespace EdsDcfNet.Tests.Writers;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Utilities;
using EdsDcfNet.Validation;

/// <summary>
/// Branches of <see cref="IniWriteRules"/> that a minimal object dictionary never reaches:
/// optional sections, DCF sub-object fields, empty keys, and stale ObjectLinks sections
/// the INI writers do not emit.
/// </summary>
public class IniWriteRulesTests
{
    [Fact]
    public void WriteToString_StaleObjectLinksAdditionalSection_ValidatedEdsWriteSucceeds()
    {
        var eds = EdsWithStaleObjectLinks();

        var written = CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        written.Should().NotContain("pwned.eds");
        written.Should().Contain("[1000ObjectLinks]");
        CanOpenFile.Eds.ReadString(written).ObjectDictionary.Objects[0x1000].ObjectLinks
            .Should().Contain((ushort)0x1018);
        CanOpenFile.Eds.WriteToString(eds).Should().NotContain("pwned.eds");
    }

    [Fact]
    public void WriteToString_StaleObjectLinksAdditionalSection_ValidatedDcfWriteSucceeds()
    {
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.ObjectDictionary.Objects[0x1000].ObjectLinks.Add(0x1018);
        dcf.AdditionalSections["1000ObjectLinks"] = StaleObjectLinksSection();

        var written = CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        written.Should().NotContain("pwned.eds");
        CanOpenFile.Dcf.ReadString(written).ObjectDictionary.Objects[0x1000].ObjectLinks
            .Should().Contain((ushort)0x1018);
    }

    [Fact]
    public void WriteToString_OrphanObjectLinksAdditionalSection_ValidatedWriteRejectsControlCharacter()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.AdditionalSections["9999ObjectLinks"] = StaleObjectLinksSection();

        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        act.Should().Throw<ModelValidationException>().Which.Issues.Should().Contain(issue =>
            issue.Path == "AdditionalSections[9999ObjectLinks].1" &&
            issue.Code == ValidationIssueCodes.IniTextNotRoundTrippable);
    }

    [Fact]
    public void WriteToString_EmptyOptionalSections_ValidatedWriteSucceeds()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.Comments = new Comments();
        eds.DynamicChannels = new DynamicChannels();

        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        act.Should().NotThrow();
    }

    [Fact]
    public void WriteToString_OmittedCommissioning_DoesNotApplyIniRulesToNodeName()
    {
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.DeviceCommissioning = new DeviceCommissioning();
        dcf.FileInfo.Description = "bad\nline";

        var act = () => CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        var issues = act.Should().Throw<ModelValidationException>().Which.Issues;
        issues.Should().Contain(issue =>
            issue.Path == "FileInfo.Description" &&
            issue.Code == ValidationIssueCodes.IniTextNotRoundTrippable);
        issues.Should().NotContain(issue => issue.Path == "DeviceCommissioning.NodeName");
    }

    [Fact]
    public void WriteToString_ValidatedEdsOptionalText_ReportsEachIniPath()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ParameterName = null!;
        eds.ObjectDictionary.Objects[0x1000].RemainingEntries["VendorNote"] = "note\n";
        eds.ObjectDictionary.Objects[0x1000].SubObjects[0x01] = new CanOpenSubObject
        {
            SubIndex = 0x01,
            ParameterName = "Sub"
        };
        eds.ObjectDictionary.Objects[0x1000].SubObjects[0x01].RemainingEntries["VendorNote"] = "sub\n";
        eds.Comments = new Comments();
        eds.Comments.CommentLines[1] = "comment\n";
        eds.SupportedModules.Add(new ModuleInfo { ProductName = "mod\n", OrderCode = "oc\n" });
        eds.DynamicChannels = new DynamicChannels();
        eds.DynamicChannels.Segments.Add(new DynamicChannelSegment { Range = "0xA000\n0xA00F" });
        eds.Tools.Add(new ToolInfo { Name = "tool\n", Command = "cmd\n" });
        eds.AdditionalSections["Vendor"] = new Dictionary<string, string>
        {
            [""] = "empty-key",
            ["Key"] = "value\n"
        };
        eds.AdditionalSections["Missing"] = null!;

        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        var issues = act.Should().Throw<ModelValidationException>().Which.Issues;
        issues.Should().Contain(issue => issue.Path == "ObjectDictionary.Objects[0x1000].RemainingEntries[VendorNote]");
        issues.Should().Contain(issue => issue.Path == "ObjectDictionary.Objects[0x1000].SubObjects[0x01].RemainingEntries[VendorNote]");
        issues.Should().Contain(issue => issue.Path == "Comments.CommentLines[1]");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].ProductName");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].OrderCode");
        issues.Should().Contain(issue => issue.Path == "DynamicChannels.Segments[0].Range");
        issues.Should().Contain(issue => issue.Path == "Tools[0].Name");
        issues.Should().Contain(issue => issue.Path == "Tools[0].Command");
        issues.Should().Contain(issue =>
            issue.Path == "AdditionalSections[Vendor]." &&
            issue.Code == ValidationIssueCodes.IniTextNotRoundTrippable);
        issues.Should().Contain(issue => issue.Path == "AdditionalSections[Vendor].Key");
        issues.Should().NotContain(issue => issue.Path.StartsWith("AdditionalSections[Missing]", StringComparison.Ordinal));
    }

    [Fact]
    public void WriteToString_ModuleFixedParameterNameNewline_UnvalidatedWriteThrowsEdsWriteException()
    {
        var eds = EdsWithModuleFixedParameterName("name\n");

        var act = () => CanOpenFile.Eds.WriteToString(eds);

        act.Should().Throw<EdsWriteException>();
    }

    [Fact]
    public void WriteToString_ValidatedModuleSectionText_ReportsEachIniPath()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        var module = new ModuleInfo { ModuleNumber = 1, ProductName = "Module", OrderCode = "OC" };
        module.Comments = new Comments { Lines = 1 };
        module.Comments.CommentLines[1] = "line\n";

        var fixedObject = new CanOpenObject
        {
            Index = 0x6423,
            ParameterName = "name\n",
            DefaultValue = "def\n",
            LowLimit = "0\n",
            ParameterValue = "pv\n",
            UploadFile = "up\n"
        };
        fixedObject.RemainingEntries["Vendor\nKey"] = "kept";
        fixedObject.RemainingEntries["VendorNote"] = "note\n";
        var sub = new CanOpenSubObject
        {
            SubIndex = 0x01,
            ParameterName = "sub\n",
            Denotation = "den\n",
            ParamRefd = "ref\n"
        };
        sub.RemainingEntries["SubNote"] = "sub\n";
        fixedObject.SubObjects[0x01] = sub;
        module.FixedObjects.Add(0x6423);
        module.FixedObjectDefinitions[0x6423] = fixedObject;

        module.SubExtends.Add(0x6000);
        module.SubExtensionDefinitions[0x6000] = new ModuleSubExtension
        {
            Index = 0x6000,
            ParameterName = "ext\n",
            DefaultValue = "d\n",
            Count = "4\n"
        };
        eds.SupportedModules.Add(module);

        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        var issues = act.Should().Throw<ModelValidationException>().Which.Issues;
        issues.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].Comments.CommentLines[1]" &&
            issue.Code == ValidationIssueCodes.IniTextNotRoundTrippable);
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].FixedObjectDefinitions[0x6423].ParameterName");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].FixedObjectDefinitions[0x6423].DefaultValue");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].FixedObjectDefinitions[0x6423].LowLimit");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].FixedObjectDefinitions[0x6423].ParameterValue");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].FixedObjectDefinitions[0x6423].UploadFile");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].FixedObjectDefinitions[0x6423].RemainingEntries[Vendor\nKey]");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].FixedObjectDefinitions[0x6423].RemainingEntries[VendorNote]");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].FixedObjectDefinitions[0x6423].SubObjects[0x01].ParameterName");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].FixedObjectDefinitions[0x6423].SubObjects[0x01].Denotation");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].FixedObjectDefinitions[0x6423].SubObjects[0x01].ParamRefd");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].FixedObjectDefinitions[0x6423].SubObjects[0x01].RemainingEntries[SubNote]");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6000].ParameterName");
        issues.Should().Contain(issue => issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6000].DefaultValue");
        issues.Should().Contain(issue =>
            issue.Path == "SupportedModules[0].SubExtensionDefinitions[0x6000].Count" &&
            issue.Code == ValidationIssueCodes.IniTextNotRoundTrippable);
    }

    [Fact]
    public void WriteToString_ValidatedDcfSubObjectFields_ReportsIniPaths()
    {
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        var sub = new CanOpenSubObject
        {
            SubIndex = 0x01,
            ParameterName = "Sub",
            ParameterValue = "pv\n",
            Denotation = "den\n",
            ParamRefd = "ref\n"
        };
        sub.RemainingEntries["VendorNote"] = "note";
        dcf.ObjectDictionary.Objects[0x1000].SubObjects[0x01] = sub;
        dcf.ObjectDictionary.Objects[0x1000].ParameterValue = "obj\n";
        dcf.ObjectDictionary.Objects[0x1000].UploadFile = "up\n";
        dcf.ObjectDictionary.Objects[0x1000].DownloadFile = "down\n";

        var act = () => CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        var issues = act.Should().Throw<ModelValidationException>().Which.Issues;
        issues.Should().Contain(issue => issue.Path == "ObjectDictionary.Objects[0x1000].SubObjects[0x01].ParameterValue");
        issues.Should().Contain(issue => issue.Path == "ObjectDictionary.Objects[0x1000].SubObjects[0x01].Denotation");
        issues.Should().Contain(issue => issue.Path == "ObjectDictionary.Objects[0x1000].SubObjects[0x01].ParamRefd");
        issues.Should().Contain(issue => issue.Path == "ObjectDictionary.Objects[0x1000].ParameterValue");
        issues.Should().Contain(issue => issue.Path == "ObjectDictionary.Objects[0x1000].UploadFile");
        issues.Should().Contain(issue => issue.Path == "ObjectDictionary.Objects[0x1000].DownloadFile");
    }

    [Fact]
    public void WriteToString_ValidatedCpjAdditionalSection_ReportsIniPath()
    {
        var cpj = ValidCanOpenModelBuilder.CreateValidCpj();
        cpj.AdditionalSections["Vendor"] = new Dictionary<string, string> { ["Key"] = "bad\n" };
        cpj.Networks[0].NetRefd = "ref\n";
        cpj.Networks[0].Nodes[2].Refd = "node\n";

        var act = () => CanOpenFile.Cpj.WriteToString(cpj, CanOpenWriteOptions.Validated);

        var issues = act.Should().Throw<ModelValidationException>().Which.Issues;
        issues.Should().Contain(issue => issue.Path == "AdditionalSections[Vendor].Key");
        issues.Should().Contain(issue => issue.Path == "Networks[0].NetRefd");
        issues.Should().Contain(issue => issue.Path == "Networks[0].Nodes[2].Refd");
    }

    [Fact]
    public void Apply_ModelOutsideIniFormats_AddsNoIssues()
    {
        var issues = new List<ValidationIssue>();

        IniWriteRules.Apply(new object(), issues);
        IniWriteRules.Apply(null!, issues);

        issues.Should().BeEmpty();
    }

    [Fact]
    public void TryReject_NullKeyOrSectionName_RejectsAsEmpty()
    {
        IniWriteRules.TryReject(null, IniWriteRules.IniTextSlot.Key, out var keyMessage).Should().BeTrue();
        keyMessage.Should().Contain("empty").And.Contain("round-trip");

        IniWriteRules.TryReject(null, IniWriteRules.IniTextSlot.SectionName, out var sectionMessage).Should().BeTrue();
        sectionMessage.Should().Contain("section name").And.Contain("round-trip");

        IniWriteRules.TryReject(null, IniWriteRules.IniTextSlot.Value, out var valueMessage).Should().BeFalse();
        valueMessage.Should().BeEmpty();

        IniWriteRules.TryReject(string.Empty, IniWriteRules.IniTextSlot.Key, out var emptyKey).Should().BeTrue();
        emptyKey.Should().Contain("empty");
        IniWriteRules.TryReject(string.Empty, IniWriteRules.IniTextSlot.SectionName, out _).Should().BeFalse();
        IniWriteRules.TryReject(string.Empty, IniWriteRules.IniTextSlot.Value, out _).Should().BeFalse();
    }

    private static ElectronicDataSheet EdsWithModuleFixedParameterName(string parameterName)
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        var module = new ModuleInfo { ModuleNumber = 1, ProductName = "Module", OrderCode = "OC" };
        module.FixedObjects.Add(0x6423);
        module.FixedObjectDefinitions[0x6423] = new CanOpenObject
        {
            Index = 0x6423,
            ParameterName = parameterName
        };
        eds.SupportedModules.Add(module);
        return eds;
    }

    private static ElectronicDataSheet EdsWithStaleObjectLinks()
    {
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.ObjectDictionary.Objects[0x1000].ObjectLinks.Add(0x1018);
        eds.AdditionalSections["1000ObjectLinks"] = StaleObjectLinksSection();
        return eds;
    }

    private static Dictionary<string, string> StaleObjectLinksSection()
    {
        return new Dictionary<string, string>
        {
            ["1"] = "Evil\n[FileInfo]\nFileName=pwned.eds"
        };
    }

    [Fact]
    public void WriteToString_AdditionalSectionNamedLikeGeneratedSection_ValidatedEdsWriteSkipsIt()
    {
        // Arrange — the writer generates [FileInfo] and skips the kept copy, so its value is not checked.
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.AdditionalSections["FileInfo"] = InvalidSection();

        // Act
        var written = CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        CountHeaders(written, "[FileInfo]").Should().Be(1);
        written.Should().NotContain("Key=");
    }

    [Fact]
    public void WriteToString_AdditionalSectionNamedLikeGeneratedSection_ValidatedDcfWriteSkipsIt()
    {
        // Arrange
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.AdditionalSections["fileinfo"] = InvalidSection();

        // Act
        var written = CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        CountHeaders(written, "[FileInfo]").Should().Be(1);
        written.Should().NotContain("Key=");
    }

    [Fact]
    public void WriteToString_NonCollidingInvalidAdditionalSection_ValidatedWriteStillRejects()
    {
        // Arrange
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.AdditionalSections["FileInfo"] = InvalidSection();
        eds.AdditionalSections["Vendor"] = InvalidSection();

        // Act
        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        act.Should().Throw<ModelValidationException>().Which.Issues.Select(issue => issue.Path)
            .Should().Contain("AdditionalSections[Vendor].Key")
            .And.NotContain("AdditionalSections[FileInfo].Key");
    }

    [Fact]
    public void WriteToString_GeneratedPartRejected_ValidatedEdsWriteChecksEveryAdditionalSection()
    {
        // Arrange — the writer would reject [DeviceInfo]; without its headers every kept section is checked.
        var eds = ValidCanOpenModelBuilder.CreateValidEds();
        eds.DeviceInfo.VendorName = "bad\nvendor";
        eds.AdditionalSections["FileInfo"] = InvalidSection();

        // Act
        var act = () => CanOpenFile.Eds.WriteToString(eds, CanOpenWriteOptions.Validated);

        // Assert
        act.Should().Throw<ModelValidationException>().Which.Issues.Select(issue => issue.Path)
            .Should().Contain("AdditionalSections[FileInfo].Key");
    }

    [Fact]
    public void WriteToString_GeneratedPartRejected_ValidatedDcfWriteChecksEveryAdditionalSection()
    {
        // Arrange
        var dcf = ValidCanOpenModelBuilder.CreateValidDcf();
        dcf.DeviceInfo.VendorName = "bad\nvendor";
        dcf.AdditionalSections["FileInfo"] = InvalidSection();

        // Act
        var act = () => CanOpenFile.Dcf.WriteToString(dcf, CanOpenWriteOptions.Validated);

        // Assert
        act.Should().Throw<ModelValidationException>().Which.Issues.Select(issue => issue.Path)
            .Should().Contain("AdditionalSections[FileInfo].Key");
    }

    private static Dictionary<string, string> InvalidSection()
        => new() { ["Key"] = "bad\nvalue" };

    private static int CountHeaders(string text, string header)
        => text.Split('\n').Count(line => string.Equals(line.TrimEnd('\r'), header, StringComparison.OrdinalIgnoreCase));
}
