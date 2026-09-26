namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet.Models;
using EdsDcfNet.Parsers;

/// <summary>
/// Ordered section storage and the plain-dictionary fallback used when a caller
/// builds sections without <see cref="IniParser"/>.
/// </summary>
public class IniSectionDictionaryTests
{
    [Fact]
    public void Set_UpdatesAnExistingKey_WithoutChangingInsertionOrder()
    {
        var section = new IniSectionDictionary();

        section.Set("Group", "First");
        section.Set("group", "Second");
        section.Set("Lang-Bemerkung", "Hinweis");

        section.Count.Should().Be(2);
        section.EntriesInOrder().Should().Equal(
            new KeyValuePair<string, string>("Group", "Second"),
            new KeyValuePair<string, string>("Lang-Bemerkung", "Hinweis"));
    }

    [Fact]
    public void ParseObjectDictionary_PlainSectionDictionary_StillCapturesUnknownKeys()
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["MandatoryObjects"] = Section(("SupportedObjects", "1"), ("1", "0x2000")),
            ["2000"] = Section(
                ("ParameterName", "Vendor Object"),
                ("Group", "Motion"),
                ("ObjectType", "0x7"),
                ("SubNumber", "1")),
            ["2000sub1"] = Section(
                ("ParameterName", "Vendor Sub"),
                ("Lang-Bemerkung", "SubHinweis"))
        };

        var dictionary = new PlainSectionReader().Parse(sections);
        var obj = dictionary.Objects[0x2000];

        obj.ParameterName.Should().Be("Vendor Object");
        obj.RemainingEntries.Keys.Should().Equal("Group");
        obj.RemainingEntries["Group"].Should().Be("Motion");
        obj.SubObjects[1].ParameterName.Should().Be("Vendor Sub");
        obj.SubObjects[1].RemainingEntries.Keys.Should().Equal("Lang-Bemerkung");
        obj.SubObjects[1].RemainingEntries["Lang-Bemerkung"].Should().Be("SubHinweis");
    }

    private static Dictionary<string, string> Section(params (string Key, string Value)[] pairs)
    {
        var section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in pairs)
            section[key] = value;
        return section;
    }

    private sealed class PlainSectionReader : CanOpenReaderBase
    {
        protected override string[] KnownSectionNames => Array.Empty<string>();

        public ObjectDictionary Parse(Dictionary<string, Dictionary<string, string>> sections)
            => ParseObjectDictionary(sections);
    }
}
