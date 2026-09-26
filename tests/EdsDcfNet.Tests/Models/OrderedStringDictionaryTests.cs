namespace EdsDcfNet.Tests.Models;

using System.Collections;
using EdsDcfNet.Models;

/// <summary>
/// Covers the public ordered map, including the non-generic dictionary surface
/// used by clone completeness and callers that only see <see cref="IDictionary"/>.
/// </summary>
public class OrderedStringDictionaryTests
{
    [Fact]
    public void DefaultComparer_IsOrdinalIgnoreCase_AndPreservesInsertionOrder()
    {
        var map = new OrderedStringDictionary();

        map.Comparer.Should().BeSameAs(StringComparer.OrdinalIgnoreCase);
        map.IsReadOnly.Should().BeFalse();
        map.Count.Should().Be(0);
        map.Keys.Should().BeEmpty();
        map.Values.Should().BeEmpty();

        map.Add("Group", "Motion");
        map.Add(new KeyValuePair<string, string>("Lang-Bemerkung", "Hinweis"));
        map["Extra"] = "1";

        map.Keys.Should().Equal("Group", "Lang-Bemerkung", "Extra");
        map.Values.Should().Equal("Motion", "Hinweis", "1");
        map.Should().Contain(new KeyValuePair<string, string>("Group", "Motion"));
        map.ContainsKey("group").Should().BeTrue();
        map.TryGetValue("LANG-BEMERKUNG", out var remark).Should().BeTrue();
        remark.Should().Be("Hinweis");
    }

    [Fact]
    public void CustomComparer_IsStoredAndUsedForLookup()
    {
        var map = new OrderedStringDictionary(StringComparer.Ordinal);

        map.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        map["Group"] = "Motion";

        map.ContainsKey("group").Should().BeFalse();
        map.TryGetValue("group", out var missing).Should().BeFalse();
        missing.Should().BeNull();
        map["group"] = "Other";
        map.Keys.Should().Equal("Group", "group");
    }

    [Fact]
    public void Indexer_UpdatesExistingKey_KeepsOriginalTextAndPosition()
    {
        var map = new OrderedStringDictionary
        {
            ["Group"] = "First",
            ["Lang"] = "Note"
        };

        map["group"] = "Second";

        map.Should().ContainSingle(entry => entry.Key == "Group").Which.Value.Should().Be("Second");
        map.Keys.Should().Equal("Group", "Lang");
        map["Group"].Should().Be("Second");
    }

    [Fact]
    public void Remove_DropsTheMatchingEntry_AndKeepsLaterKeys()
    {
        var map = new OrderedStringDictionary
        {
            ["Group"] = "Motion",
            ["Lang"] = "Note",
            ["Extra"] = "1"
        };

        map.Remove("missing").Should().BeFalse();
        map.Remove(new KeyValuePair<string, string>("Lang", "nope")).Should().BeFalse();
        map.Remove(new KeyValuePair<string, string>("lang", "Note")).Should().BeTrue();
        map.Remove("GROUP").Should().BeTrue();

        map.Keys.Should().Equal("Extra");
        map.Should().NotContain(new KeyValuePair<string, string>("Extra", "2"));
        map.Contains(new KeyValuePair<string, string>("Extra", "1")).Should().BeTrue();
        map.Contains(default(KeyValuePair<string, string>)).Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesEveryEntry()
    {
        var map = new OrderedStringDictionary { ["Group"] = "Motion" };

        map.Clear();

        map.Should().BeEmpty();
        map.ContainsKey("Group").Should().BeFalse();
    }

    [Fact]
    public void CopyTo_WritesEntriesInInsertionOrder()
    {
        var map = new OrderedStringDictionary
        {
            ["Group"] = "Motion",
            ["Lang"] = "Note"
        };
        var pairs = new KeyValuePair<string, string>[3];

        map.CopyTo(pairs, 1);

        pairs[0].Should().Be(default(KeyValuePair<string, string>));
        pairs[1].Should().Be(new KeyValuePair<string, string>("Group", "Motion"));
        pairs[2].Should().Be(new KeyValuePair<string, string>("Lang", "Note"));

        var entries = new DictionaryEntry[2];
        ((ICollection)map).CopyTo(entries, 0);
        entries[0].Should().Be(new DictionaryEntry("Group", "Motion"));
        entries[1].Should().Be(new DictionaryEntry("Lang", "Note"));

        var empty = new OrderedStringDictionary();
        var none = Array.Empty<KeyValuePair<string, string>>();
        empty.CopyTo(none, 0);
        ((ICollection)empty).CopyTo(Array.Empty<DictionaryEntry>(), 0);
    }

    [Fact]
    public void NonGenericDictionary_RoundTripsEntries()
    {
        IDictionary map = new OrderedStringDictionary();

        map.IsFixedSize.Should().BeFalse();
        map.IsReadOnly.Should().BeFalse();
        ((ICollection)map).IsSynchronized.Should().BeFalse();
        ((ICollection)map).SyncRoot.Should().BeSameAs(map);
        map.Contains(42).Should().BeFalse();
        map.Contains("missing").Should().BeFalse();

        map.Add("Group", "Motion");
        map["Lang"] = "Note";
        map["group"] = "Updated";

        map["Group"].Should().Be("Updated");
        map.Contains("LANG").Should().BeTrue();
        map.Keys.Cast<string>().Should().Equal("Group", "Lang");
        map.Values.Cast<string>().Should().Equal("Updated", "Note");

        var enumerated = new List<DictionaryEntry>();
        foreach (DictionaryEntry entry in map)
            enumerated.Add(entry);
        enumerated.Should().Equal(
            new DictionaryEntry("Group", "Updated"),
            new DictionaryEntry("Lang", "Note"));

        map.Remove("lang");
        map.Remove(42);
        map.Keys.Cast<string>().Should().Equal("Group");

        ((IEnumerable)map).GetEnumerator().MoveNext().Should().BeTrue();
    }

    [Fact]
    public void DictionaryEnumerator_ResetsAndRejectsCurrentOutsideTheRange()
    {
        IDictionary map = new OrderedStringDictionary { ["Group"] = "Motion" };
        var enumerator = map.GetEnumerator();

        var before = () => enumerator.Entry;
        before.Should().Throw<InvalidOperationException>();
        var beforeKey = () => enumerator.Key;
        beforeKey.Should().Throw<InvalidOperationException>();

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Entry.Should().Be(new DictionaryEntry("Group", "Motion"));
        enumerator.Key.Should().Be("Group");
        enumerator.Value.Should().Be("Motion");
        enumerator.Current.Should().Be(enumerator.Entry);
        enumerator.MoveNext().Should().BeFalse();
        enumerator.Key.Should().Be("Group");

        enumerator.Reset();
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Key.Should().Be("Group");

        var empty = new OrderedStringDictionary();
        ((IDictionary)empty).GetEnumerator().MoveNext().Should().BeFalse();
    }

    [Fact]
    public void NullAndInvalidArguments_AreRejected()
    {
        var construct = () => new OrderedStringDictionary(null!);
        construct.Should().Throw<ArgumentNullException>().WithParameterName("comparer");

        var map = new OrderedStringDictionary { ["Group"] = "Motion" };

        var getNull = () => map[null!];
        getNull.Should().Throw<ArgumentNullException>().WithParameterName("key");

        var setNullKey = () => map[null!] = "x";
        setNullKey.Should().Throw<ArgumentNullException>().WithParameterName("key");

        var setNullValue = () => map["Group"] = null!;
        setNullValue.Should().Throw<ArgumentNullException>().WithParameterName("value");

        var addNullKey = () => map.Add(null!, "x");
        addNullKey.Should().Throw<ArgumentNullException>().WithParameterName("key");

        var addNullValue = () => map.Add("Lang", null!);
        addNullValue.Should().Throw<ArgumentNullException>().WithParameterName("value");

        var duplicate = () => map.Add("group", "Other");
        duplicate.Should().Throw<ArgumentException>();

        var missing = () => map["missing"];
        missing.Should().Throw<KeyNotFoundException>();

        var containsNull = () => map.ContainsKey(null!);
        containsNull.Should().Throw<ArgumentNullException>().WithParameterName("key");

        var removeNull = () => map.Remove(null!);
        removeNull.Should().Throw<ArgumentNullException>().WithParameterName("key");

        var tryNull = () => map.TryGetValue(null!, out _);
        tryNull.Should().Throw<ArgumentNullException>().WithParameterName("key");

        var copyNull = () => map.CopyTo(null!, 0);
        copyNull.Should().Throw<ArgumentNullException>().WithParameterName("array");

        var copyNegative = () => map.CopyTo(new KeyValuePair<string, string>[1], -1);
        copyNegative.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("arrayIndex");

        var copyPastEnd = () => map.CopyTo(new KeyValuePair<string, string>[1], 2);
        copyPastEnd.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("arrayIndex");

        var copyTooSmall = () => map.CopyTo(new KeyValuePair<string, string>[1], 1);
        copyTooSmall.Should().Throw<ArgumentException>().WithParameterName("array");

        IDictionary nongeneric = map;
        var indexNull = () => nongeneric[null!];
        indexNull.Should().Throw<ArgumentNullException>().WithParameterName("key");

        var indexWrongType = () => nongeneric[42];
        indexWrongType.Should().Throw<ArgumentException>().WithParameterName("key");

        var indexMissing = () => nongeneric["missing"];
        indexMissing.Should().Throw<KeyNotFoundException>();

        var setWrongKey = () => nongeneric[42] = "x";
        setWrongKey.Should().Throw<ArgumentException>().WithParameterName("key");

        var setNullNongenericValue = () => nongeneric["Lang"] = null;
        setNullNongenericValue.Should().Throw<ArgumentNullException>().WithParameterName("value");

        var setWrongValue = () => nongeneric["Lang"] = 42;
        setWrongValue.Should().Throw<ArgumentException>().WithParameterName("value");

        var addWrongKey = () => nongeneric.Add(42, "x");
        addWrongKey.Should().Throw<ArgumentException>().WithParameterName("key");

        var addNullNongeneric = () => nongeneric.Add(null!, "x");
        addNullNongeneric.Should().Throw<ArgumentNullException>().WithParameterName("key");

        var containsNullKey = () => nongeneric.Contains(null!);
        containsNullKey.Should().Throw<ArgumentNullException>().WithParameterName("key");

        var removeNullKey = () => nongeneric.Remove(null!);
        removeNullKey.Should().Throw<ArgumentNullException>().WithParameterName("key");

        ICollection collection = map;
        var copyCollectionNull = () => collection.CopyTo(null!, 0);
        copyCollectionNull.Should().Throw<ArgumentNullException>().WithParameterName("array");

        var copyRank = () => collection.CopyTo(new string[1, 1], 0);
        copyRank.Should().Throw<ArgumentException>().WithParameterName("array");

        var copyCollectionNegative = () => collection.CopyTo(new DictionaryEntry[1], -1);
        copyCollectionNegative.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("index");

        var copyCollectionTooSmall = () => collection.CopyTo(new DictionaryEntry[1], 1);
        copyCollectionTooSmall.Should().Throw<ArgumentException>().WithParameterName("array");
    }
}
