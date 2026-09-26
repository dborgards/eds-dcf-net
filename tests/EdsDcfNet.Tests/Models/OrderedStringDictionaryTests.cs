namespace EdsDcfNet.Tests.Models;

using System.Collections;
using EdsDcfNet.Models;

/// <summary>
/// Insertion order, case rules, and both dictionary interfaces of
/// <see cref="OrderedStringDictionary"/>.
/// </summary>
public class OrderedStringDictionaryTests
{
    [Fact]
    public void Constructor_Default_UsesOrdinalIgnoreCase()
    {
        var map = new OrderedStringDictionary();

        map.Comparer.Should().BeSameAs(StringComparer.OrdinalIgnoreCase);
        map.Count.Should().Be(0);
        map.IsReadOnly.Should().BeFalse();
        map.Keys.Should().BeEmpty();
        map.Values.Should().BeEmpty();
        ((IDictionary)map).IsFixedSize.Should().BeFalse();
        ((ICollection)map).IsSynchronized.Should().BeFalse();
        ((ICollection)map).SyncRoot.Should().BeSameAs(map);
    }

    [Fact]
    public void Constructor_CustomComparer_IsStored()
    {
        var map = new OrderedStringDictionary(StringComparer.Ordinal);

        map.Comparer.Should().BeSameAs(StringComparer.Ordinal);
        map.Add("A", "upper");
        map.Add("a", "lower");

        map.Keys.Should().Equal("A", "a");
        map["A"].Should().Be("upper");
        map["a"].Should().Be("lower");
    }

    [Fact]
    public void Constructor_NullComparer_Throws()
    {
        var act = () => new OrderedStringDictionary(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("comparer");
    }

    [Fact]
    public void Indexer_SetAndGet_PreservesInsertionOrderAndOriginalKeyText()
    {
        var map = new OrderedStringDictionary
        {
            ["Group"] = "First",
            ["Lang"] = "Hinweis"
        };

        map["group"] = "Second";
        map["Extra"] = "tail";

        map.Keys.Should().Equal("Group", "Lang", "Extra");
        map.Values.Should().Equal("Second", "Hinweis", "tail");
        map["GROUP"].Should().Be("Second");
        map.Select(entry => (entry.Key, entry.Value)).Should().Equal(
            ("Group", "Second"),
            ("Lang", "Hinweis"),
            ("Extra", "tail"));
    }

    [Fact]
    public void Indexer_MissingOrNullKey_Throws()
    {
        var map = new OrderedStringDictionary { ["Group"] = "Motion" };

        var missing = () => map["Other"];
        var nullGet = () => map[null!];
        var nullSet = () => map[null!] = "x";
        var nullValue = () => map["Group"] = null!;

        missing.Should().Throw<KeyNotFoundException>();
        nullGet.Should().Throw<ArgumentNullException>().WithParameterName("key");
        nullSet.Should().Throw<ArgumentNullException>().WithParameterName("key");
        nullValue.Should().Throw<ArgumentNullException>().WithParameterName("value");
    }

    [Fact]
    public void Add_DuplicateNullAndPair_FollowsDictionaryRules()
    {
        var map = new OrderedStringDictionary();
        map.Add("Group", "Motion");
        map.Add(new KeyValuePair<string, string>("Lang", "Hinweis"));

        var duplicate = () => map.Add("group", "Again");
        var nullKey = () => map.Add(null!, "x");
        var nullValue = () => map.Add("Other", null!);
        var nullPair = () => map.Add(new KeyValuePair<string, string>("Other", null!));

        duplicate.Should().Throw<ArgumentException>();
        nullKey.Should().Throw<ArgumentNullException>().WithParameterName("key");
        nullValue.Should().Throw<ArgumentNullException>().WithParameterName("value");
        nullPair.Should().Throw<ArgumentNullException>().WithParameterName("value");
        map.Keys.Should().Equal("Group", "Lang");
    }

    [Fact]
    public void Contains_MatchesKeyAndOrdinalValue()
    {
        var map = new OrderedStringDictionary { ["Group"] = "Motion" };

        map.Contains(new KeyValuePair<string, string>("group", "Motion")).Should().BeTrue();
        map.Contains(new KeyValuePair<string, string>("Group", "motion")).Should().BeFalse();
        map.Contains(default(KeyValuePair<string, string>)).Should().BeFalse();
        map.ContainsKey("GROUP").Should().BeTrue();
        map.ContainsKey("Other").Should().BeFalse();

        var nullKey = () => map.ContainsKey(null!);
        nullKey.Should().Throw<ArgumentNullException>().WithParameterName("key");
    }

    [Fact]
    public void TryGetValue_HitMissAndNull()
    {
        var map = new OrderedStringDictionary { ["Group"] = "Motion" };

        map.TryGetValue("group", out var found).Should().BeTrue();
        found.Should().Be("Motion");
        map.TryGetValue("Other", out var missing).Should().BeFalse();
        missing.Should().BeNull();

        var act = () => map.TryGetValue(null!, out _);
        act.Should().Throw<ArgumentNullException>().WithParameterName("key");
    }

    [Fact]
    public void Remove_ByKeyOrPair_DropsTheEntryAndKeepsOrder()
    {
        var map = new OrderedStringDictionary
        {
            ["A"] = "1",
            ["B"] = "2",
            ["C"] = "3"
        };

        map.Remove(new KeyValuePair<string, string>("B", "nope")).Should().BeFalse();
        map.Remove(new KeyValuePair<string, string>("b", "2")).Should().BeTrue();
        map.Remove("missing").Should().BeFalse();
        map.Remove(default(KeyValuePair<string, string>)).Should().BeFalse();
        map.Remove("c").Should().BeTrue();

        map.Keys.Should().Equal("A");
        map["A"].Should().Be("1");

        var act = () => map.Remove(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("key");
    }

    [Fact]
    public void Clear_RemovesKeysAndValues()
    {
        var map = new OrderedStringDictionary { ["Group"] = "Motion" };

        map.Clear();

        map.Should().BeEmpty();
        map.Count.Should().Be(0);
        map.Keys.Should().BeEmpty();
        map.Values.Should().BeEmpty();
    }

    [Fact]
    public void CopyTo_WritesPairsInOrder()
    {
        var map = new OrderedStringDictionary { ["A"] = "1", ["B"] = "2" };
        var pairs = new KeyValuePair<string, string>[4];

        map.CopyTo(pairs, 1);

        pairs[0].Should().Be(default(KeyValuePair<string, string>));
        pairs[1].Should().Be(new KeyValuePair<string, string>("A", "1"));
        pairs[2].Should().Be(new KeyValuePair<string, string>("B", "2"));
        pairs[3].Should().Be(default(KeyValuePair<string, string>));
    }

    [Fact]
    public void CopyTo_EmptyMapAtEndOfArray_Succeeds()
    {
        var map = new OrderedStringDictionary();
        var pairs = new KeyValuePair<string, string>[1];

        var act = () => map.CopyTo(pairs, pairs.Length);

        act.Should().NotThrow();
        pairs[0].Should().Be(default(KeyValuePair<string, string>));
    }

    [Fact]
    public void CopyTo_InvalidDestination_Throws()
    {
        var map = new OrderedStringDictionary { ["A"] = "1", ["B"] = "2" };

        var nullArray = () => map.CopyTo(null!, 0);
        var negative = () => map.CopyTo(new KeyValuePair<string, string>[2], -1);
        var pastEnd = () => map.CopyTo(new KeyValuePair<string, string>[2], 3);
        var tooSmall = () => map.CopyTo(new KeyValuePair<string, string>[1], 0);

        nullArray.Should().Throw<ArgumentNullException>().WithParameterName("array");
        negative.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("arrayIndex");
        pastEnd.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("arrayIndex");
        tooSmall.Should().Throw<ArgumentException>().WithParameterName("array");
    }

    [Fact]
    public void NonGenericIndexer_MissingStringKey_ReturnsNull()
    {
        IDictionary dictionary = new OrderedStringDictionary { ["Present"] = "value" };

        dictionary["Present"].Should().Be("value");
        dictionary["present"].Should().Be("value");
        dictionary["missing"].Should().BeNull();
    }

    [Fact]
    public void NonGenericIndexer_NonStringOrNullKey_Throws()
    {
        IDictionary dictionary = new OrderedStringDictionary { ["Present"] = "value" };

        var wrongType = () => dictionary[42];
        var nullKey = () => dictionary[null!];

        var wrongTypeException = wrongType.Should().Throw<ArgumentException>().Which;
        wrongTypeException.Should().BeOfType<ArgumentException>();
        wrongTypeException.ParamName.Should().Be("key");
        nullKey.Should().Throw<ArgumentNullException>().WithParameterName("key");
    }

    [Fact]
    public void NonGenericIndexer_Set_UpdatesOrAppends()
    {
        IDictionary dictionary = new OrderedStringDictionary { ["Present"] = "value" };

        dictionary["present"] = "updated";
        dictionary["Extra"] = "tail";

        var map = (OrderedStringDictionary)dictionary;
        map.Keys.Should().Equal("Present", "Extra");
        map["Present"].Should().Be("updated");
        map["Extra"].Should().Be("tail");
    }

    [Fact]
    public void NonGenericIndexer_SetInvalidEntry_Throws()
    {
        IDictionary dictionary = new OrderedStringDictionary();

        var nullKey = () => { dictionary[null!] = "x"; };
        var wrongKey = () => { dictionary[42] = "x"; };
        var nullValue = () => { dictionary["K"] = null; };
        var wrongValue = () => { dictionary["K"] = 1; };

        nullKey.Should().Throw<ArgumentNullException>().WithParameterName("key");
        AssertExactArgumentException(wrongKey, "key");
        nullValue.Should().Throw<ArgumentNullException>().WithParameterName("value");
        AssertExactArgumentException(wrongValue, "value");
    }

    [Fact]
    public void NonGenericAddContainsAndRemove_AcceptOnlyStringKeys()
    {
        IDictionary dictionary = new OrderedStringDictionary { ["Group"] = "Motion" };

        dictionary.Contains("group").Should().BeTrue();
        dictionary.Contains("other").Should().BeFalse();
        dictionary.Contains(42).Should().BeFalse();

        var nullContains = () => dictionary.Contains(null!);
        nullContains.Should().Throw<ArgumentNullException>().WithParameterName("key");

        dictionary.Add("Lang", "Hinweis");
        var duplicate = () => dictionary.Add("lang", "Again");
        var nullKey = () => dictionary.Add(null!, "x");
        var wrongKey = () => dictionary.Add(42, "x");
        var nullValue = () => dictionary.Add("Other", null);
        var wrongValue = () => dictionary.Add("Other", 1);

        duplicate.Should().Throw<ArgumentException>();
        nullKey.Should().Throw<ArgumentNullException>().WithParameterName("key");
        AssertExactArgumentException(wrongKey, "key");
        nullValue.Should().Throw<ArgumentNullException>().WithParameterName("value");
        AssertExactArgumentException(wrongValue, "value");

        dictionary.Remove(42);
        dictionary.Remove("group");
        dictionary.Contains("Group").Should().BeFalse();
        dictionary.Contains("Lang").Should().BeTrue();

        var nullRemove = () => dictionary.Remove(null!);
        nullRemove.Should().Throw<ArgumentNullException>().WithParameterName("key");
    }

    [Fact]
    public void NonGenericKeysValuesAndCopyTo_FollowInsertionOrder()
    {
        IDictionary dictionary = new OrderedStringDictionary { ["A"] = "1", ["B"] = "2" };

        dictionary.Keys.Cast<string>().Should().Equal("A", "B");
        dictionary.Values.Cast<string>().Should().Equal("1", "2");

        var array = new DictionaryEntry[4];
        ((ICollection)dictionary).CopyTo(array, 1);

        array[0].Should().Be(default(DictionaryEntry));
        array[1].Should().Be(new DictionaryEntry("A", "1"));
        array[2].Should().Be(new DictionaryEntry("B", "2"));
        array[3].Should().Be(default(DictionaryEntry));
    }

    [Fact]
    public void NonGenericCopyTo_EmptyMapAtEndOfArray_Succeeds()
    {
        ICollection collection = new OrderedStringDictionary();
        var array = new DictionaryEntry[1];

        var act = () => collection.CopyTo(array, array.Length);

        act.Should().NotThrow();
    }

    [Fact]
    public void NonGenericCopyTo_InvalidDestination_Throws()
    {
        ICollection collection = new OrderedStringDictionary { ["A"] = "1" };
        var multiRank = new DictionaryEntry[1, 1];

        var nullArray = () => collection.CopyTo(null!, 0);
        var rank = () => collection.CopyTo(multiRank, 0);
        var negative = () => collection.CopyTo(new DictionaryEntry[1], -1);
        var tooSmall = () => collection.CopyTo(new DictionaryEntry[1], 1);

        nullArray.Should().Throw<ArgumentNullException>().WithParameterName("array");
        rank.Should().Throw<ArgumentException>().WithParameterName("array");
        negative.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("index");
        tooSmall.Should().Throw<ArgumentException>().WithParameterName("array");
    }

    [Fact]
    public void Enumerators_WalkInsertionOrderAndRejectCurrentBeforeStart()
    {
        var map = new OrderedStringDictionary { ["A"] = "1", ["B"] = "2" };

        IEnumerator generic = ((IEnumerable)map).GetEnumerator();
        generic.MoveNext().Should().BeTrue();
        generic.Current.Should().Be(new KeyValuePair<string, string>("A", "1"));

        var enumerator = ((IDictionary)map).GetEnumerator();
        var beforeStart = () => enumerator.Entry;
        beforeStart.Should().Throw<InvalidOperationException>();
        var beforeKey = () => enumerator.Key;
        beforeKey.Should().Throw<InvalidOperationException>();
        var beforeValue = () => enumerator.Value;
        beforeValue.Should().Throw<InvalidOperationException>();
        var beforeCurrent = () => enumerator.Current;
        beforeCurrent.Should().Throw<InvalidOperationException>();

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Key.Should().Be("A");
        enumerator.Value.Should().Be("1");
        enumerator.Entry.Should().Be(new DictionaryEntry("A", "1"));
        enumerator.Current.Should().Be(new DictionaryEntry("A", "1"));

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Key.Should().Be("B");
        enumerator.MoveNext().Should().BeFalse();

        enumerator.Reset();
        var afterReset = () => enumerator.Current;
        afterReset.Should().Throw<InvalidOperationException>();
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Key.Should().Be("A");
    }

    [Fact]
    public void NonGenericEnumerator_EmptyMap_MoveNextIsFalse()
    {
        var enumerator = ((IDictionary)new OrderedStringDictionary()).GetEnumerator();

        enumerator.MoveNext().Should().BeFalse();
        var current = () => enumerator.Current;
        current.Should().Throw<InvalidOperationException>();
        enumerator.Reset();
        enumerator.MoveNext().Should().BeFalse();
    }

    private static void AssertExactArgumentException(Action act, string parameterName)
    {
        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.Should().BeOfType<ArgumentException>();
        exception.ParamName.Should().Be(parameterName);
    }
}
