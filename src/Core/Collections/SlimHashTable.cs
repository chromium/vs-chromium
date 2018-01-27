// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Collections {
  public class SlimHashTable<TKey, TValue> : IEnumerable<TValue> {
    private static readonly double DefaultLoadFactor = 2.0;
    private readonly Func<TValue, TKey> _getKey;
    private readonly Action _locker;
    private readonly Action _unlocker;
    private readonly double _loadFactor;
    private readonly IEqualityComparer<TKey> _comparer;
    private Entry[] _entries;
    private int _count;
    private int _mask;
    private Overflow _overflow;

    public SlimHashTable(ISlimHashTableParameters<TKey, TValue> parameters)
      : this(parameters, 0, DefaultLoadFactor, EqualityComparer<TKey>.Default) {
    }

    public SlimHashTable(ISlimHashTableParameters<TKey, TValue> parameters, int capacity)
      : this(parameters, capacity, DefaultLoadFactor, EqualityComparer<TKey>.Default) {
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
      _loadFactor = loadFactor;
      _comparer = comparer;
      _mask = GetMask(capacity, loadFactor);
      _entries = new Entry[_mask + 1];
      _overflow = new Overflow();
    }

    private static int GetMask(int capacity, double loadFactor) {
      var targetCapacity = (int)(capacity / loadFactor);
      for (var mask = 32; mask < int.MaxValue; mask = mask * 2) {
        if (targetCapacity < mask) {
          return mask - 1;
        }
      }
      throw Invariants.Fail("Dictionary capacity is greater than int.MaxValue");
    }

    public int Count => _count;

    public TValue this[TKey key] {
      get { return Get(key); }
      set { GetOrAdd(key, value); }
    }

    public void Add(TValue value) {
      Add(_getKey(value), value);
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

    public void Clear() {
      using (new Locker(this)) {
        _mask = 31;
        _entries = new Entry[_mask + 1];
      }
    }

    public bool Contains(TKey key) {
      var comparer = _comparer;
      var hashCode = comparer.GetHashCode(key);

      using (new Locker(this)) {
        return FindEntryLocation(key, hashCode) != null;
      }
    }

    public TValue Get(TKey key) {
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
        foreach (var entry in this) {
          array[index] = convert(_getKey(entry), entry);
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

        int index = location.Value.Index;
        int overflowIndex = location.Value.OverflowIndex;
        int previousOverflowIndex = location.Value.PreviousOverflowIndex;
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

    public IEnumerator<TValue> GetEnumerator() {
      foreach (var e in _entries) {
        for (var head = e; head.IsValid;) {
          yield return head.Value;
          if (head.OverflowIndex >= 0) {
            head = _overflow.Get(head.OverflowIndex);
          } else {
            break;
          }
        }
      }
    }

    private TValue AddEntry(TValue value, int hashCode) {
      var index = hashCode & _mask;
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
      int newMask = _mask * 2 + 1;
      var oldEntries = _entries;
      var newEntries = new Entry[newMask + 1];
      var newOverflow = new Overflow();

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

      _entries = newEntries;
      _overflow = newOverflow;
      _mask = newMask;
    }

    private void StoreNewEntry(Entry[] newEntries, Overflow newOverflow, TValue value) {
      int newMask = newEntries.Length - 1;
      var newIndex = _comparer.GetHashCode(_getKey(value)) & newMask;
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
      int index = hashCode & _mask;
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

    private struct EntryLocation {
      private readonly int _index;
      private readonly int _previousOverflowIndex;
      private readonly int _overflowIndex;

      public EntryLocation(int index, int previousOverflowIndex, int overflowIndex) {
        _index = index;
        _previousOverflowIndex = previousOverflowIndex;
        _overflowIndex = overflowIndex;
      }

      public int Index {
        get { return _index; }
      }

      public int PreviousOverflowIndex {
        get { return _previousOverflowIndex; }
      }

      public int OverflowIndex {
        get { return _overflowIndex; }
      }
    }

    private class Overflow {
      private readonly List<Entry> _entries = new List<Entry>();
      private int _freeListHead;
      private int _freeListSize;

      public Overflow() {
        _freeListHead = -1;
      }

      public Entry Get(int index) {
        return _entries[index];
      }

      public void Free(int index) {
        _entries[index] = new Entry(default(TValue), _freeListHead);
        _freeListHead = index;
        _freeListSize++;
      }

      public void SetOverflowIndex(int index, int overflowIndex) {
        _entries[index] = new Entry(_entries[index].Value, overflowIndex);
      }

      public int Allocate() {
        if (_freeListHead < 0) {
          Invariants.Assert(_freeListSize == 0);
          Grow();
        }

        var index = _freeListHead;
        _freeListHead = _entries[index].OverflowIndex;
        _entries[index] = default(Entry);
        _freeListSize--;
        return index;
      }

      private void Grow() {
        var oldLen = _entries.Count;
        var newLen = Math.Max(2, _entries.Count * 2);
        for (var i = oldLen; i < newLen; i++) {
          var nextIndex = (i + 1 < newLen) ? i + 1 : -1;
          _entries.Add(new Entry(default(TValue), nextIndex));
        }
        _freeListHead = oldLen;
        _freeListSize += newLen - oldLen;
      }

      public void Set(int index, Entry entry) {
        _entries[index] = entry;
      }
    }

    private struct Entry {
      public readonly TValue Value;
      /// <summary>
      /// 0 = Invalid entry (<code>null</code>)
      /// -1 = No overflow index
      /// [1.. n] == overflow index [0..n -1]
      /// </summary>
      private readonly int _overflowIndex; // index + 1, so that 0 means "invalid"

      internal Entry(TValue value, int overflowIndex) {
        Value = value;
        _overflowIndex = overflowIndex == -1 ? -1 : overflowIndex + 1;
      }

      /// <summary>
      /// Index into overflow buffer. <code>-1</code> if last entry in the chain.
      /// </summary>
      public int OverflowIndex {
        get {
          Invariants.Assert(IsValid, "Overflow index is not valid");
          return _overflowIndex == -1 ? -1 : _overflowIndex - 1;
        }
      }

      public bool IsValid => _overflowIndex != 0;

      public override string ToString() {
        return $"Value={Value} - OverflowIndex={(IsValid ? OverflowIndex.ToString() : "n/a")}";
      }
    }

    private struct Locker : IDisposable {
      private readonly SlimHashTable<TKey, TValue> _table;

      public Locker(SlimHashTable<TKey, TValue> table) {
        _table = table;
        _table._locker();
      }

      public void Dispose() {
        _table._unlocker();
      }
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }
  }
}