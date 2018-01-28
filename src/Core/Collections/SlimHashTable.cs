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
    private readonly int _capacity;
    private readonly double _loadFactor;
    private readonly IEqualityComparer<TKey> _comparer;
    private readonly object _syncRoot = new object();
    private int[] _slots;
    private Entry[] _entries;
    private int _count;
    private int _freeListHead;
    private KeyCollection _keys;
    private ValueCollection _values;

    public SlimHashTable(ISlimHashTableParameters<TKey, TValue> parameters)
      : this(parameters, 0, DefaultLoadFactor, EqualityComparer<TKey>.Default) {
    }

    public SlimHashTable(ISlimHashTableParameters<TKey, TValue> parameters, int capacity, double loadFactor)
      : this(parameters, capacity, loadFactor, EqualityComparer<TKey>.Default) {
    }

    public SlimHashTable(ISlimHashTableParameters<TKey, TValue> parameters, int capacity, double loadFactor,
      IEqualityComparer<TKey> comparer) {
      Invariants.CheckArgumentNotNull(parameters, nameof(parameters));
      Invariants.CheckArgument(loadFactor >= 0.1, "Load factor is too small", nameof(loadFactor));
      Invariants.CheckArgumentNotNull(comparer, nameof(comparer));

      _getKey = parameters.KeyGetter;
      _capacity = capacity;
      _loadFactor = loadFactor;
      _comparer = comparer;

      var length = GetPrimeLength(capacity, loadFactor);
      InitSlots(length);
    }

    private void InitSlots(int length) {
      _slots = new int[length];
      for (var i = 0; i < _slots.Length; i++) {
        _slots[i] = -1;
      }
      _entries = new Entry[length];
      _freeListHead = -1;
      _count = 0;
    }

    public static SlimHashTable<TKey, TValue> Create(Func<TValue, TKey> keygetter, int capacity) {
      return new SlimHashTable<TKey, TValue>(new Parameters(keygetter), capacity, 1.0, EqualityComparer<TKey>.Default);
    }

    public static SlimHashTable<TKey, TValue> Create(Func<TValue, TKey> keygetter, int capacity,
      IEqualityComparer<TKey> comparer) {
      return new SlimHashTable<TKey, TValue>(new Parameters(keygetter), capacity, 1.0, comparer);
    }

    private class Parameters : ISlimHashTableParameters<TKey, TValue> {
      public Parameters(Func<TValue, TKey> keygetter) {
        KeyGetter = keygetter;
      }

      public Func<TValue, TKey> KeyGetter { get; }
    }

    private static int GetPrimeLength(int capacity, double loadFactor) {
      var targetCapacity = (int) (capacity / loadFactor);
      return HashCode.GetPrime(targetCapacity);
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

    public ICollection<TKey> Keys => _keys ?? (_keys = new KeyCollection(this));

    public ICollection<TValue> Values => _values ?? (_values = new ValueCollection(this));

    public object SyncRoot => _syncRoot;

    public void Add(TValue value) {
      Add(_getKey(value), value);
    }

    public void Add(KeyValuePair<TKey, TValue> item) {
      Add(item.Key, item.Value);
    }

    public void Add(TKey key, TValue value) {
      AddEntry(key, value, false);
    }

    public bool ContainsKey(TKey key) {
      return Contains(key);
    }

    public void Clear() {
      var length = GetPrimeLength(_capacity, _loadFactor);
      InitSlots(length);
    }

    public bool Contains(KeyValuePair<TKey, TValue> item) {
      return ContainsKey(item.Key);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
      CopyTo(array, (k, v) => new KeyValuePair<TKey, TValue>(k, v), arrayIndex);
    }

    public bool Contains(TKey key) {
      var hashCode = _comparer.GetHashCode(key);
      return FindEntryLocation(key, hashCode).IsValid;
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
      var hashCode = _comparer.GetHashCode(key);
      var location = FindEntryLocation(key, hashCode);
      if (location.IsValid) {
        value = _entries[location.EntryIndex].Value;
        return true;
      }

      value = default(TValue);
      return false;
    }

    public TValue GetOrAdd(TKey key, TValue value) {
      var hashCode = _comparer.GetHashCode(key);
      var slotIndex = (hashCode & int.MaxValue) % _slots.Length;
      for (var entryIndex = _slots[slotIndex]; entryIndex >= 0;) {
        var entry = _entries[entryIndex];
        if (entry.HashCode == hashCode && _comparer.Equals(key, _getKey(entry.Value))) {
          return entry.Value;
        }
        entryIndex = _entries[entryIndex].NextIndex;
      }

      AddEntryWorker(value, slotIndex, hashCode);
      return value;
    }

    public void CopyTo<TArray>(TArray[] array, Func<TKey, TValue, TArray> convert, int arrayIndex) {
      var index = arrayIndex;
      foreach (var kvp in this) {
        array[index] = convert(kvp.Key, kvp.Value);
        index++;
      }
    }

    public bool Remove(TKey key) {
      var hashCode = _comparer.GetHashCode(key);
      var location = FindEntryLocation(key, hashCode);
      if (!location.IsValid) {
        return false;
      }

      if (location.PreviousEntryIndex >= 0) {
        // Entry is inside list, not the head
        _entries[location.PreviousEntryIndex].SetNextIndex(_entries[location.EntryIndex].NextIndex);
      } else {
        // Entry is first from slot
        _slots[location.SlotIndex] = _entries[location.EntryIndex].NextIndex;
      }
      _entries[location.EntryIndex].Value = default(TValue);
      _entries[location.EntryIndex].SetNextFreeIndex(_freeListHead);
      _freeListHead = location.EntryIndex;
      _count--;
      return true;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
      for (var i = 0; i < _count; i++) {
        var entry = _entries[i];
        if (entry.IsValid) {
          yield return new KeyValuePair<TKey, TValue>(_getKey(entry.Value), entry.Value);
        }
      }
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }

    private void AddEntry(TKey key, TValue value, bool updateExistingAllowed) {
      var hashCode = _comparer.GetHashCode(key);
      var slotIndex = (hashCode & int.MaxValue) % _slots.Length;
      for (var entryIndex = _slots[slotIndex]; entryIndex >= 0;) {
        var entry = _entries[entryIndex];
        if (entry.HashCode == hashCode && _comparer.Equals(key, _getKey(entry.Value))) {
          if (!updateExistingAllowed) {
            Invariants.CheckArgument(false, nameof(key), "Key already exist in table");
          }
          _entries[entryIndex].Value = value;
          return;
        }
        entryIndex = _entries[entryIndex].NextIndex;
      }

      AddEntryWorker(value, slotIndex, hashCode);
    }

    private void AddEntryWorker(TValue value, int slotIndex, int hashCode) {
      // We need to actually add the value
      if (_count >= _slots.Length) {
        Grow();
        slotIndex = (hashCode & int.MaxValue) % _slots.Length;
      }

      // Insert at head of list starting at (new) slotIndex
      int newEntryIndex;
      if (_freeListHead >= 0) {
        newEntryIndex = _freeListHead;
        _freeListHead = _entries[_freeListHead].NextFreeIndex;
      } else {
        // Free list is empty, so insertion is at end of list of _entries
        newEntryIndex = _count;
      }

      _entries[newEntryIndex] = new Entry(value, hashCode, _slots[slotIndex]);
      _slots[slotIndex] = newEntryIndex;
      _count++;
    }

    private void Grow() {
      Invariants.Assert(_count == _entries.Length);
      Invariants.Assert(_count == _slots.Length);
      Invariants.Assert(_freeListHead== -1);
      var oldEntries = _entries;
      var oldLength = _entries.Length;

      var newLength = HashCode.GetPrime(GrowSize(oldLength));
      var newSlots = new int[newLength];
      for (var i = 0; i < newSlots.Length; i++) {
        newSlots[i] = -1;
      }
      var newEntries = new Entry[newLength];
      Array.Copy(_entries, newEntries, _entries.Length);

      for (var entryIndex = 0; entryIndex < oldEntries.Length; entryIndex++) {
        var oldEntry = oldEntries[entryIndex];
        if (oldEntry.IsValid) {
          var newSlotIndex = (oldEntry.HashCode & int.MaxValue) % newLength;
          newEntries[entryIndex].SetNextIndex(newSlots[newSlotIndex]);
          newSlots[newSlotIndex] = entryIndex;
        }
      }

      _slots = newSlots;
      _entries = newEntries;
    }

    private static int GrowSize(int oldLength) {
      return Math.Max(2, (int) Math.Round((double) oldLength * 4 / 2));
    }

    private EntryLocation FindEntryLocation(TKey key, int hashCode) {
      var slotIndex = (hashCode & int.MaxValue) % _slots.Length;
      var previousEntryIndex = -1;
      for (var entryIndex = _slots[slotIndex]; entryIndex >= 0; ) {
        var entry = _entries[entryIndex];
        if (_comparer.Equals(key, _getKey(entry.Value))) {
          return new EntryLocation(slotIndex, entryIndex, previousEntryIndex);
        }

        previousEntryIndex = entryIndex;
        entryIndex = _entries[entryIndex].NextIndex;
      }

      return new EntryLocation(-1, -1, -1);
    }
  }
}