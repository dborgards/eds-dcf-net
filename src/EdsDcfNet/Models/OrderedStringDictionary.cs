namespace EdsDcfNet.Models;

using System.Collections;

/// <summary>
/// Insertion-ordered, case-insensitive string map.
/// </summary>
/// <remarks>
/// Enumeration follows insertion order on every target framework.
/// <see cref="Dictionary{TKey,TValue}"/> does not promise that order on .NET Framework,
/// which is why unknown EDS/DCF keys are not stored in a plain dictionary.
/// The default comparer is <see cref="StringComparer.OrdinalIgnoreCase"/> (CiA 306).
/// Updating an existing key, including a key that differs only by case, replaces the
/// value and keeps the original key text and position.
/// The non-generic <see cref="System.Collections.IDictionary"/> indexer returns
/// <see langword="null"/> when a string key is absent.
/// </remarks>
public sealed class OrderedStringDictionary : IDictionary<string, string>, IDictionary
{
    private readonly IEqualityComparer<string> _comparer;
    private readonly Dictionary<string, string> _lookup;
    private readonly List<string> _order = new();

    /// <summary>
    /// Creates an empty map that compares keys with <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// </summary>
    public OrderedStringDictionary()
        : this(StringComparer.OrdinalIgnoreCase)
    {
    }

    /// <summary>
    /// Creates an empty map that compares keys with <paramref name="comparer"/>.
    /// </summary>
    /// <param name="comparer">Key comparer. <see langword="null"/> is rejected.</param>
    public OrderedStringDictionary(IEqualityComparer<string> comparer)
    {
        ThrowIfNull(comparer, nameof(comparer));

        _comparer = comparer;
        _lookup = new Dictionary<string, string>(comparer);
    }

    /// <summary>
    /// Gets the comparer used for keys.
    /// </summary>
    public IEqualityComparer<string> Comparer => _comparer;

    /// <summary>
    /// Gets the number of entries.
    /// </summary>
    public int Count => _lookup.Count;

    /// <summary>
    /// Gets a value indicating whether the map is read-only. Always <see langword="false"/>.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets the keys in insertion order. Membership tests use <see cref="Comparer"/>,
    /// so <c>Keys.Contains</c> agrees with <see cref="ContainsKey"/>.
    /// </summary>
    public ICollection<string> Keys => new KeyCollection(this);

    /// <summary>
    /// Gets the values in insertion order.
    /// </summary>
    public ICollection<string> Values
    {
        get
        {
            var values = new string[_order.Count];
            for (var i = 0; i < _order.Count; i++)
                values[i] = _lookup[_order[i]];
            return values;
        }
    }

    /// <summary>
    /// Gets or sets the value for <paramref name="key"/>.
    /// A new key is appended. An existing key, matched by <see cref="Comparer"/>, keeps its
    /// original text and position.
    /// </summary>
    /// <param name="key">Entry key.</param>
    /// <returns>The stored value.</returns>
    public string this[string key]
    {
        get
        {
            ThrowIfNull(key, nameof(key));
            return _lookup[key];
        }
        set
        {
            ThrowIfNull(key, nameof(key));
            ThrowIfNull(value, nameof(value));

            if (_lookup.ContainsKey(key))
            {
                _lookup[key] = value;
                return;
            }

            _lookup.Add(key, value);
            _order.Add(key);
        }
    }

    /// <summary>
    /// Adds an entry. Throws if <paramref name="key"/> is already present.
    /// </summary>
    /// <param name="key">Entry key.</param>
    /// <param name="value">Entry value.</param>
    public void Add(string key, string value)
    {
        ThrowIfNull(key, nameof(key));
        ThrowIfNull(value, nameof(value));

        _lookup.Add(key, value);
        _order.Add(key);
    }

    /// <inheritdoc/>
    public void Add(KeyValuePair<string, string> item) => Add(item.Key, item.Value);

    /// <summary>
    /// Removes every entry.
    /// </summary>
    public void Clear()
    {
        _lookup.Clear();
        _order.Clear();
    }

    /// <inheritdoc/>
    public bool Contains(KeyValuePair<string, string> item)
        => item.Key is not null
           && TryGetValue(item.Key, out var value)
           && string.Equals(value, item.Value, StringComparison.Ordinal);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="key"/> is present.
    /// </summary>
    /// <param name="key">Entry key.</param>
    public bool ContainsKey(string key)
    {
        ThrowIfNull(key, nameof(key));
        return _lookup.ContainsKey(key);
    }

    /// <inheritdoc/>
    public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
    {
        ThrowIfNull(array, nameof(array));
        if ((uint)arrayIndex > (uint)array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < Count)
            throw new ArgumentException("Destination array is not long enough.", nameof(array));

        foreach (var entry in this)
            array[arrayIndex++] = entry;
    }

    /// <summary>
    /// Returns an enumerator in insertion order.
    /// </summary>
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        foreach (var key in _order)
            yield return new KeyValuePair<string, string>(key, _lookup[key]);
    }

    /// <summary>
    /// Removes <paramref name="key"/> when it is present.
    /// </summary>
    /// <param name="key">Entry key.</param>
    /// <returns><see langword="true"/> when an entry was removed.</returns>
    public bool Remove(string key)
    {
        ThrowIfNull(key, nameof(key));
        if (!_lookup.Remove(key))
            return false;

        for (var i = 0; i < _order.Count; i++)
        {
            if (_comparer.Equals(_order[i], key))
            {
                _order.RemoveAt(i);
                break;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public bool Remove(KeyValuePair<string, string> item)
    {
        if (!Contains(item))
            return false;
        return Remove(item.Key);
    }

    /// <summary>
    /// Gets the value for <paramref name="key"/>.
    /// </summary>
    /// <param name="key">Entry key.</param>
    /// <param name="value">The stored value when the key is present; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the key is present.</returns>
    public bool TryGetValue(string key, out string value)
    {
        ThrowIfNull(key, nameof(key));

        if (_lookup.TryGetValue(key, out var found))
        {
            value = found;
            return true;
        }

        value = null!;
        return false;
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    bool IDictionary.IsFixedSize => false;

    ICollection IDictionary.Keys => new KeyCollection(this);

    ICollection IDictionary.Values
    {
        get
        {
            var values = new string[_order.Count];
            for (var i = 0; i < _order.Count; i++)
                values[i] = _lookup[_order[i]];
            return values;
        }
    }

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    object? IDictionary.this[object key]
    {
        get
        {
            if (key is not string typed)
            {
                ThrowIfNull(key, nameof(key));
                throw new ArgumentException("Key must be a string.", nameof(key));
            }

            return _lookup.TryGetValue(typed, out var value) ? value : null;
        }
        set
        {
            this[RequireKey(key)] = RequireValue(value);
        }
    }

    void IDictionary.Add(object key, object? value) => Add(RequireKey(key), RequireValue(value));

    bool IDictionary.Contains(object key)
    {
        if (key is not string typed)
        {
            ThrowIfNull(key, nameof(key));
            return false;
        }

        return _lookup.ContainsKey(typed);
    }

    IDictionaryEnumerator IDictionary.GetEnumerator() => new DictionaryEnumerator(this);

    void IDictionary.Remove(object key)
    {
        if (key is string typed)
            Remove(typed);
        else
            ThrowIfNull(key, nameof(key));
    }

    void ICollection.CopyTo(Array array, int index)
    {
        ThrowIfNull(array, nameof(array));
        if (array.Rank != 1)
            throw new ArgumentException("Array must be one-dimensional.", nameof(array));
#if NET10_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(index);
#else
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
#endif
        if (array.Length - index < Count)
            throw new ArgumentException("Destination array is not long enough.", nameof(array));

        foreach (var entry in this)
            array.SetValue(new DictionaryEntry(entry.Key, entry.Value), index++);
    }

    private static string RequireKey(object key)
    {
        if (key is not string typed)
        {
            ThrowIfNull(key, nameof(key));
            throw new ArgumentException("Key must be a string.", nameof(key));
        }

        return typed;
    }

    private static string RequireValue(object? value)
    {
        if (value is not string typed)
        {
            ThrowIfNull(value, nameof(value));
            throw new ArgumentException("Value must be a string.", nameof(value));
        }

        return typed;
    }

    private static void ThrowIfNull(object? value, string parameterName)
    {
        if (value is null)
            throw new ArgumentNullException(parameterName);
    }

    /// <summary>
    /// Insertion-ordered key view. Membership uses the parent comparer, including the
    /// non-generic <see cref="System.Collections.IList"/> implementation returned from
    /// <see cref="IDictionary.Keys"/>.
    /// </summary>
    private sealed class KeyCollection : IList<string>, System.Collections.IList
    {
        private readonly OrderedStringDictionary _dictionary;

        public KeyCollection(OrderedStringDictionary dictionary) => _dictionary = dictionary;

        public int Count => _dictionary._order.Count;

        public bool IsReadOnly => true;

        public bool IsFixedSize => true;

        string IList<string>.this[int index]
        {
            get => _dictionary._order[index];
            set => throw new NotSupportedException();
        }

        public bool Contains(string item) => _dictionary.ContainsKey(item);

        public int IndexOf(string item)
        {
            ThrowIfNull(item, nameof(item));
            var comparer = _dictionary._comparer;
            var order = _dictionary._order;
            for (var i = 0; i < order.Count; i++)
            {
                if (comparer.Equals(order[i], item))
                    return i;
            }

            return -1;
        }

        public void CopyTo(string[] array, int arrayIndex)
        {
            ThrowIfNull(array, nameof(array));
            if ((uint)arrayIndex > (uint)array.Length)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            if (array.Length - arrayIndex < Count)
                throw new ArgumentException("Destination array is not long enough.", nameof(array));

            foreach (var key in _dictionary._order)
                array[arrayIndex++] = key;
        }

        public IEnumerator<string> GetEnumerator()
        {
            foreach (var key in _dictionary._order)
                yield return key;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(string item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public void Insert(int index, string item) => throw new NotSupportedException();

        public bool Remove(string item) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();

        bool System.Collections.ICollection.IsSynchronized => false;

        object System.Collections.ICollection.SyncRoot => _dictionary;

        object? System.Collections.IList.this[int index]
        {
            get => _dictionary._order[index];
            set => throw new NotSupportedException();
        }

        int System.Collections.IList.Add(object? value) => throw new NotSupportedException();

        bool System.Collections.IList.Contains(object? value) => IndexOfNonGeneric(value) >= 0;

        int System.Collections.IList.IndexOf(object? value) => IndexOfNonGeneric(value);

        void System.Collections.IList.Insert(int index, object? value) => throw new NotSupportedException();

        void System.Collections.IList.Remove(object? value) => throw new NotSupportedException();

        void System.Collections.ICollection.CopyTo(Array array, int index)
        {
            ThrowIfNull(array, nameof(array));
            if (array.Rank != 1)
                throw new ArgumentException("Array must be one-dimensional.", nameof(array));
#if NET10_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfNegative(index);
#else
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif
            if (array.Length - index < Count)
                throw new ArgumentException("Destination array is not long enough.", nameof(array));

            foreach (var key in _dictionary._order)
                array.SetValue(key, index++);
        }

        private int IndexOfNonGeneric(object? value)
        {
            if (value is not string typed)
            {
                ThrowIfNull(value, nameof(value));
                return -1;
            }

            return IndexOf(typed);
        }
    }

    private sealed class DictionaryEnumerator : IDictionaryEnumerator
    {
        private readonly KeyValuePair<string, string>[] _entries;
        private int _index = -1;

        public DictionaryEnumerator(OrderedStringDictionary dictionary)
        {
            _entries = new KeyValuePair<string, string>[dictionary.Count];
            var i = 0;
            foreach (var entry in dictionary)
                _entries[i++] = entry;
        }

        public DictionaryEntry Entry => new(CurrentPair.Key, CurrentPair.Value);

        public object Key => CurrentPair.Key;

        public object? Value => CurrentPair.Value;

        public object Current => Entry;

        public bool MoveNext()
        {
            if (_index + 1 >= _entries.Length)
                return false;

            _index++;
            return true;
        }

        public void Reset() => _index = -1;

        private KeyValuePair<string, string> CurrentPair
        {
            get
            {
                if ((uint)_index >= (uint)_entries.Length)
                    throw new InvalidOperationException("Enumeration has not started or has finished.");
                return _entries[_index];
            }
        }
    }
}
