// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;

namespace VsChromium.Core.Collections {
  public partial class SlimHashTable<TKey, TValue> : IDictionary<TKey, TValue> {
    private static readonly double DefaultLoadFactor = 2.0;
    private readonly Func<TValue, TKey> _getKey;
    private readonly Action _locker;
    private readonly Action _unlocker;
    private readonly int _capacity;
    private readonly double _loadFactor;
    private readonly IEqualityComparer<TKey> _comparer;
    private readonly object _syncRoot = new object();
    private Entry[] _entries;
    private int _count;
    private int _length;
    private Overflow _overflow;
    private KeyCollection _keys;
    private ValueCollection _values;

    public SlimHashTable(ISlimHashTableParameters<TKey, TValue> parameters)
      : this(parameters, 0, DefaultLoadFactor, EqualityComparer<TKey>.Default) {
    }

    public SlimHashTable(ISlimHashTableParameters<TKey, TValue> parameters, int capacity, double loadFactor)
      : this(parameters, capacity, loadFactor, EqualityComparer<TKey>.Default) {
    }

    public SlimHashTable(ISlimHashTableParameters<TKey, TValue> parameters, int capacity, double loadFactor, IEqualityComparer<TKey> comparer) {
      Invariants.CheckArgumentNotNull(parameters, nameof(parameters));
      Invariants.CheckArgument(loadFactor >= 0.1, "Load factor is too small", nameof(loadFactor));
      Invariants.CheckArgumentNotNull(comparer, nameof(comparer));

      _getKey = parameters.KeyGetter;
      _locker = parameters.Locker;
      _unlocker = parameters.Unlnlocker;
      _capacity = capacity;
      _loadFactor = loadFactor;
      _comparer = comparer;
      _length = GetPrimeLength(capacity, loadFactor);
      _entries = new Entry[_length];
      _overflow = new Overflow(GetOverflowCapacity(capacity, loadFactor));
    }

    private static int GetPrimeLength(int capacity, double loadFactor) {
      var targetCapacity = (int)(capacity / loadFactor);
      return HashCode.GetPrime(targetCapacity);
    }

    private static int GetOverflowCapacity(int capacity, double loadFactor) {
      var targetCapacity = (int)(capacity / loadFactor);
      return Math.Max(0, capacity - targetCapacity);
    }

    public bool Remove(KeyValuePair<TKey, TValue> item) {
      return Remove(item.Key);
    }

    public int Count => _count;
    public bool IsReadOnly => false;

    public TValue this[TKey key] {
      get { return GetOrDefault(key); }
      set { GetOrAdd(key, value); }
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

    public object SyncRoot {
      get { return _syncRoot; }
    }

    public void Add(TValue value) {
      Add(_getKey(value), value);
    }

    public bool ContainsKey(TKey key) {
      return Contains(key);
    }

    public void Add(TKey key, TValue value) {
      var comparer = _comparer;
      var hashCode = comparer.GetHashCode(key);

      using (new Locker(this)) {
        var location = FindEntryLocation(key, hashCode);
        if (location != null) {
          Invariants.CheckArgument(false, nameof(key), "Key already exists in dictionary");
        }

        AddEntry(value, hashCode);
      }
    }

    public void Add(KeyValuePair<TKey, TValue> item) {
      Add(item.Key, item.Value);
    }

    public void Clear() {
      using (new Locker(this)) {
        _count = 0;
        _length = GetPrimeLength(_capacity, _loadFactor);
        _entries = new Entry[_length];
        _overflow = new Overflow(GetOverflowCapacity(_capacity, _loadFactor));
      }
    }

    public bool Contains(KeyValuePair<TKey, TValue> item) {
      return ContainsKey(item.Key);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
      CopyTo(array, (k,v) => new KeyValuePair<TKey, TValue>(k, v), arrayIndex);
    }

    public bool Contains(TKey key) {
      var comparer = _comparer;
      var hashCode = comparer.GetHashCode(key);

      using (new Locker(this)) {
        return FindEntryLocation(key, hashCode) != null;
      }
    }

    private bool ContainsValue(TValue item) {
      return ContainsKey(_getKey(item));
    }

    public TValue GetOrDefault(TKey key) {
      TValue result;
      if (!TryGetValue(key, out result)) {
        return default(TValue);
      }
      return result;
    }

    public bool TryGetValue(TKey key, out TValue value) {
      var comparer = _comparer;
      var hashCode = comparer.GetHashCode(key);

      using (new Locker(this)) {
        var location = FindEntryLocation(key, hashCode);
        if (location != null) {
          value = location.Value.OverflowIndex >= 0
            ? _overflow.Get(location.Value.OverflowIndex).Value
            : _entries[location.Value.Index].Value;
          return true;
        }
      }

      value = default(TValue);
      return false;
    }

    public TValue GetOrAdd(TKey key, TValue value) {
      var comparer = _comparer;
      var hashCode = comparer.GetHashCode(key);

      using (new Locker(this)) {
        var location = FindEntryLocation(key, hashCode);
        if (location != null) {
          value = location.Value.OverflowIndex >= 0
            ? _overflow.Get(location.Value.OverflowIndex).Value
            : _entries[location.Value.Index].Value;
          return value;
        }
        return AddEntry(value, hashCode);
      }
    }

    public void CopyTo<TArray>(TArray[] array, Func<TKey, TValue, TArray> convert, int arrayIndex) {
      using (new Locker(this)) {
        var index = arrayIndex;
        foreach (var kvp in this) {
          array[index] = convert(kvp.Key, kvp.Value);
          index++;
        }
      }
    }

    public bool Remove(TKey key) {
      var comparer = _comparer;
      var hashCode = comparer.GetHashCode(key);

      using (new Locker(this)) {
        var location = FindEntryLocation(key, hashCode);
        if (location == null) {
          return false;
        }

        var index = location.Value.Index;
        var overflowIndex = location.Value.OverflowIndex;
        var previousOverflowIndex = location.Value.PreviousOverflowIndex;
        if (overflowIndex >= 0) {
          if (previousOverflowIndex >= 0) {
            // Remove entry contained in the overflow table
            var entry = _overflow.Get(overflowIndex);
            _overflow.Free(overflowIndex);
            _overflow.SetOverflowIndex(previousOverflowIndex, entry.OverflowIndex);

          } else {
            // entry is first overflow entry
            var entry = _overflow.Get(overflowIndex);
            _overflow.Free(overflowIndex);
            _entries[index] = new Entry(_entries[index].Value, entry.OverflowIndex);
          }

        } else {
          var entry = _entries[index];
          if (entry.OverflowIndex >= 0) {
            var overflowEntry = _overflow.Get(entry.OverflowIndex);
            _entries[index] = overflowEntry;
            _overflow.Free(entry.OverflowIndex);
          } else {
            _entries[index] = default(Entry);
          }
        }

        _count--;
        return true;
      }
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
      foreach (var e in _entries) {
        for (var head = e; head.IsValid;) {
          yield return new KeyValuePair<TKey, TValue>(_getKey(head.Value), head.Value);
          if (head.OverflowIndex >= 0) {
            head = _overflow.Get(head.OverflowIndex);
          } else {
            break;
          }
        }
      }
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }

    private TValue AddEntry(TValue value, int hashCode) {
      var index = (hashCode & int.MaxValue) % _length;
      var entry = _entries[index];
      if (entry.IsValid) {
        var overflowIndex = _overflow.Allocate();
        _overflow.Set(overflowIndex, entry);
        _entries[index] = new Entry(value, overflowIndex);
      } else {
        _entries[index] = new Entry(value, -1);
      }
      _count++;
      if ((float)_count / _entries.Length >= _loadFactor) {
        Grow();
      }

      return value;
    }

    private void Grow() {
      var oldEntries = _entries;
      var oldLength = _entries.Length;

      var newLength = HashCode.GetPrime(GrowSize(oldLength));
      var newEntries = new Entry[newLength];
      var newOverflow = new Overflow(Math.Max(_overflow.Capacity, _overflow.Count - (newLength - oldLength)));

      // use oldEntries.Length to eliminate the rangecheck            
      for (var i = 0; i < oldEntries.Length; i++) {
        var e = oldEntries[i];
        if (e.IsValid) {
          StoreNewEntry(newEntries, newOverflow, e.Value);
          for(var overflowIndex = e.OverflowIndex; overflowIndex >= 0; overflowIndex = e.OverflowIndex) {
            e = _overflow.Get(overflowIndex);
            Invariants.Assert(e.IsValid);
            _overflow.Free(overflowIndex);
            StoreNewEntry(newEntries, newOverflow, e.Value);
          }
        }
      }

      _length = newLength;
      _entries = newEntries;
      _overflow = newOverflow;
    }

    private static int GrowSize(int oldLength) {
      return Math.Max(2, (int)Math.Round((double)oldLength * 3 / 2));
    }

    private void StoreNewEntry(Entry[] newEntries, Overflow newOverflow, TValue value) {
      var newLength = newEntries.Length;
      var newIndex = (_comparer.GetHashCode(_getKey(value)) & int.MaxValue) % newLength;
      var newEntry = newEntries[newIndex];
      if (newEntry.IsValid) {
        // Store at head of overflow list
        var overflowIndex = newOverflow.Allocate();
        newOverflow.Set(overflowIndex, newEntry);
        newEntries[newIndex] = new Entry(value, overflowIndex);
      } else {
        // Store in entry
        newEntries[newIndex] = new Entry(value, -1);
      }
    }

    private EntryLocation? FindEntryLocation(TKey key, int hashCode) {
      var index = (hashCode & int.MaxValue) % _length;
      var entry = _entries[index];
      if (!entry.IsValid) {
        return null;
      }

      if (_comparer.Equals(key, _getKey(entry.Value))) {
        return new EntryLocation(index, -1, -1);
      }

      var previousOverflowIndex = -1;
      var overflowIndex = entry.OverflowIndex;
      while (true) {
        if (overflowIndex < 0) {
          return null;
        }

        entry = _overflow.Get(overflowIndex);
        if (!entry.IsValid) {
          return null;
        }

        if (_comparer.Equals(key, _getKey(entry.Value))) {
          return new EntryLocation(index, previousOverflowIndex, overflowIndex);
        }

        previousOverflowIndex = overflowIndex;
        overflowIndex = entry.OverflowIndex;
      }
    }
  }
}