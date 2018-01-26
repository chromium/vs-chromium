using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VsChromium.Core.Collections {
  public class SlimDictionary<TKey, TValue> : IDictionary<TKey, TValue> {
    private static readonly double DefaultLoadFactor = 3.0;
    private readonly object _syncRoot = new object();
    private readonly SlimHashTable<TKey, Entry> _table;
    private KeyCollection _keys;
    private ValueCollection _values;

    public SlimDictionary() : this(0, EqualityComparer<TKey>.Default) {
    }

    public SlimDictionary(int capacity) : this(capacity, EqualityComparer<TKey>.Default) {
    }

    public SlimDictionary(int capacity, IEqualityComparer<TKey> comparer) {
      _table = new SlimHashTable<TKey, Entry>(new Parameters(), capacity, DefaultLoadFactor, comparer);
    }

    public ICollection<TKey> Keys {
      get {
        if (_keys == null) {
          _keys = new KeyCollection(this);
        }

        return _keys;
      }
    }

    public ICollection<TValue> Values {
      get {
        if (_values == null) {
          _values = new ValueCollection(this);
        }

        return _values;
      }
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
      return _table.Select(x => new KeyValuePair<TKey, TValue>(x.Key, x.Value)).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }

    public void Add(KeyValuePair<TKey, TValue> item) {
      _table.Add(item.Key, new Entry(item.Key, item.Value));
    }

    public void Clear() {
      _table.Clear();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item) {
      return _table.Contains(item.Key);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
      _table.CopyTo(array, (k, v) => new KeyValuePair<TKey, TValue>(k, v.Value), arrayIndex);
    }

    public bool Remove(KeyValuePair<TKey, TValue> item) {
      return _table.Remove(item.Key);
    }

    public int Count {
      get { return _table.Count; }
    }

    public bool IsReadOnly {
      get { return false; }
    }

    public object SyncRoot {
      get { return _syncRoot; }
    }

    public bool ContainsKey(TKey key) {
      return _table.Contains(key);
    }

    public bool ContainsValue(TValue value) {
      if (ReferenceEquals(value, null)) {
        foreach (var entry in this) {
          if (entry.Value == null)
            return true;
        }
      } else {
        var equalityComparer = EqualityComparer<TValue>.Default;
        foreach (var entry in this) {
          if (equalityComparer.Equals(entry.Value, value)) {
            return true;
          }
        }
      }

      return false;
    }

    public void Add(TKey key, TValue value) {
      _table.Add(key, new Entry(key, value));
    }

    public bool Remove(TKey key) {
      return _table.Remove(key);
    }

    public bool TryGetValue(TKey key, out TValue value) {
      Entry entry;
      if (_table.TryGetValue(key, out entry)) {
        value = entry.Value;
        return true;
      }

      value = default(TValue);
      return false;
    }

    public TValue this[TKey key] {
      get { return _table[key].Value; }
      set { _table[key] = new Entry(key, value); }
    }

    private class KeyCollection : ICollection<TKey>, ICollection, IReadOnlyCollection<TKey> {
      private readonly SlimDictionary<TKey, TValue> _dictionary;

      public KeyCollection(SlimDictionary<TKey, TValue> dictionary) {
        _dictionary = dictionary;
      }

      public IEnumerator<TKey> GetEnumerator() {
        return _dictionary.Select(x => x.Key).GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
      }

      public void Add(TKey item) {
        throw new NotSupportedException();
      }

      public void Clear() {
        throw new NotSupportedException();
      }

      public bool Contains(TKey item) {
        // ReSharper disable once AssignNullToNotNullAttribute
        return _dictionary.ContainsKey(item);
      }

      public void CopyTo(TKey[] array, int arrayIndex) {
        foreach (var e in _dictionary) {
          array[arrayIndex++] = e.Key;
        }
      }

      public bool Remove(TKey item) {
        throw new NotSupportedException();
      }

      public void CopyTo(Array array, int index) {
        TKey[] array1 = array as TKey[];
        if (array1 != null) {
          CopyTo(array1, index);
        } else {
          object[] objArray = array as object[];
          if (objArray == null) {
            throw new ArgumentException("Invalid array", nameof(array));
          }

          foreach (var e in _dictionary) {
            objArray[index++] = e.Key;
          }
        }
      }

      public int Count {
        get { return _dictionary.Count; }
      }

      public object SyncRoot {
        get { return _dictionary.SyncRoot; }
      }
      public bool IsSynchronized {
        get { return false; }
      }
      public bool IsReadOnly {
        get { return true; }
      }
    }

    private class ValueCollection : ICollection<TValue>, ICollection, IReadOnlyCollection<TValue> {
      private readonly SlimDictionary<TKey, TValue> _dictionary;

      public ValueCollection(SlimDictionary<TKey, TValue> dictionary) {
        _dictionary = dictionary;
      }

      public IEnumerator<TValue> GetEnumerator() {
        return _dictionary.Select(x => x.Value).GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
      }

      public void Add(TValue item) {
        throw new NotSupportedException();
      }

      public void Clear() {
        throw new NotSupportedException();
      }

      public bool Contains(TValue item) {
        return _dictionary.ContainsValue(item);
      }

      public void CopyTo(TValue[] array, int arrayIndex) {
        foreach (var e in _dictionary) {
          array[arrayIndex++] = e.Value;
        }
      }

      public bool Remove(TValue item) {
        throw new NotSupportedException();
      }

      public void CopyTo(Array array, int index) {
        TValue[] array1 = array as TValue[];
        if (array1 != null) {
          CopyTo(array1, index);
        } else {
          object[] objArray = array as object[];
          if (objArray == null) {
            throw new ArgumentException("Invalid array", nameof(array));
          }

          foreach (var e in _dictionary) {
            objArray[index++] = e.Value;
          }
        }
      }

      public int Count {
        get { return _dictionary.Count; }
      }

      public object SyncRoot {
        get { return _dictionary.SyncRoot; }
      }
      public bool IsSynchronized {
        get { return false; }
      }
      public bool IsReadOnly {
        get { return true; }
      }
    }

    private struct Entry {
      internal readonly TKey Key;
      internal readonly TValue Value;

      public Entry(TKey key, TValue value) {
        Key = key;
        Value = value;
      }
    }

    private class Parameters : ISlimHashTableParameters<TKey, Entry> {
      public Func<Entry, TKey> KeyGetter {
        get { return t => t.Key; }
      }

      public Action Locker {
        get { return () => { }; }
      }

      public Action Unlnlocker {
        get { return () => { }; }
      }
    }
  }
}