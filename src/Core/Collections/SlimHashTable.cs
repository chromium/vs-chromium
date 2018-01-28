// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;

namespace VsChromium.Core.Collections {
  /// <summary>
  /// Implementation of <see cref="IDictionary{TKey,TValue}"/> using a a similar technique
  /// as <see cref="Dictionary{TKey,TValue}"/>, i.e. chaining is handled by storing
  /// 2 arrays: buckets to store the index of the first item in the chain, and
  /// entries to store the items (chained with a "next index" value).
  /// </summary>
  public partial class SlimHashTable<TKey, TValue> : IDictionary<TKey, TValue> {
    // ReSharper disable once StaticMemberInGenericType
    public static readonly ExponentialGrowthPolicy DefaultGrowthPolicy = ExponentialGrowthPolicy.Default;

    private readonly Func<TValue, TKey> _getKey;
    private readonly int _capacity;
    private readonly IEqualityComparer<TKey> _comparer;
    private readonly ICollectionGrowthPolicy _growthPolicy;
    private readonly object _syncRoot = new object();
    /// <summary>
    /// Each entry is the "head" of the chain of entries whose hashcode maps to the entry.
    /// The "head" value is either -1, meaning that chain is empty, or an index into
    /// the <see cref="_entries"/> table (pointing to the first entry of the chain).
    /// <para>
    /// Note that for performance reason, we actually store the "head" index value
    /// with a <code>+1</code> offset, so that the range of values [-1, 0, ..., length -1] 
    /// is actually stored as [0, 1, ..., length].</para>
    /// <para>
    /// The reason we do this is so that we don't have to initialize the array 
    /// with "-1"  values (.NET arrays are initialized with 0 values).</para>
    /// <para>
    /// This implies reading a value from this array should be offset with a "-1"
    /// (see <see cref="AdjustBucketIndexRead"/>, and writing should be offset with a "+1"
    /// (see <see cref="AdjustBucketIndexWrite"/>).</para>
    /// </summary>
    private int[] _buckets;
    private Entry[] _entries;
    private int _count;
    private int _freeListHead;
    private KeyCollection _keys;
    private ValueCollection _values;

    public SlimHashTable(Func<TValue, TKey> keyProvider)
      : this(keyProvider, 0, EqualityComparer<TKey>.Default, DefaultGrowthPolicy) {
    }

    public SlimHashTable(Func<TValue, TKey> keyProvider, ICollectionGrowthPolicy growthPolicy)
      : this(keyProvider, 0, EqualityComparer<TKey>.Default, growthPolicy) {
    }

    public SlimHashTable(Func<TValue, TKey> keyProvider, int capacity)
      : this(keyProvider, capacity, EqualityComparer<TKey>.Default, DefaultGrowthPolicy) {
    }

    public SlimHashTable(Func<TValue, TKey> keyProvider, int capacity, IEqualityComparer<TKey> comparer)
      : this(keyProvider, capacity, comparer, DefaultGrowthPolicy) {
    }

    public SlimHashTable(Func<TValue, TKey> keyProvider, int capacity, ICollectionGrowthPolicy growthPolicy)
      : this(keyProvider, capacity, EqualityComparer<TKey>.Default, growthPolicy) {
    }

    public SlimHashTable(Func<TValue, TKey> keyProvider, IEqualityComparer<TKey> comparer)
      : this(keyProvider, 0, comparer, DefaultGrowthPolicy) {
    }

    public SlimHashTable(Func<TValue, TKey> keyProvider, IEqualityComparer<TKey> comparer, ICollectionGrowthPolicy growthPolicy)
      : this(keyProvider, 0, comparer, growthPolicy) {
    }

    public SlimHashTable(Func<TValue, TKey> keyProvider, int capacity, IEqualityComparer<TKey> comparer, ICollectionGrowthPolicy growthPolicy) {
      Invariants.CheckArgumentNotNull(keyProvider, nameof(keyProvider));
      Invariants.CheckArgument(capacity >= 0, nameof(capacity), "Capacity must be positive");
      Invariants.CheckArgumentNotNull(comparer, nameof(comparer));
      Invariants.CheckArgumentNotNull(growthPolicy, nameof(growthPolicy));

      _getKey = keyProvider;
      _capacity = capacity;
      _comparer = comparer;
      _growthPolicy = growthPolicy;

      var length = GetPrimeLength(capacity);
      Initialize(length);
    }

    private void Initialize(int length) {
      _buckets = new int[length];
      _entries = new Entry[length];
      _freeListHead = -1;
      _count = 0;
    }

    public int Count => _count;

    public bool IsReadOnly => false;

    public TValue this[TKey key] {
      get {
        TValue value;
        if (!TryGetValue(key, out value)) {
          Invariants.CheckArgument(false, nameof(key), "Key not found in table");
        }
        return value;
      }
      set { UpdateOrAdd(key, value); }
    }

    public ICollection<TKey> Keys => _keys ?? (_keys = new KeyCollection(this));

    public ICollection<TValue> Values => _values ?? (_values = new ValueCollection(this));

    public object SyncRoot => _syncRoot;

    public void Clear() {
      var length = GetPrimeLength(_capacity);
      Initialize(length);
    }

    public void Add(TValue value) {
      Add(_getKey(value), value);
    }

    public void Add(KeyValuePair<TKey, TValue> item) {
      Add(item.Key, item.Value);
    }

    public void Add(TKey key, TValue value) {
      AddEntry(key, value, false);
    }

    public bool Remove(TKey key) {
      var hashCode = _comparer.GetHashCode(key);
      var bucketIndex = GetBucketIndex(hashCode, _buckets.Length);
      var previousEntryIndex = -1;
      for (var entryIndex = AdjustBucketIndexRead(_buckets[bucketIndex]); entryIndex >= 0;) {
        var entry = _entries[entryIndex];
        if (_comparer.Equals(key, _getKey(entry.Value))) {
          // Entry was found, remove it
          if (previousEntryIndex >= 0) {
            // Entry is inside list, not the head
            _entries[previousEntryIndex].SetNextIndex(_entries[entryIndex].NextIndex);
          } else {
            // Entry is first from bucket
            _buckets[bucketIndex] = AdjustBucketIndexWrite(_entries[entryIndex].NextIndex);
          }
          _entries[entryIndex].Value = default(TValue);
          _entries[entryIndex].SetNextFreeIndex(_freeListHead);
          _freeListHead = entryIndex;
          _count--;
          return true;
        }

        previousEntryIndex = entryIndex;
        entryIndex = _entries[entryIndex].NextIndex;
      }
      return false;
    }

    public bool Remove(KeyValuePair<TKey, TValue> item) {
      return Remove(item.Key);
    }

    public bool ContainsKey(TKey key) {
      TValue result;
      return TryGetValue(key, out result);
    }

    public bool ContainsValue(TValue item) {
      return ContainsKey(_getKey(item));
    }

    public bool Contains(KeyValuePair<TKey, TValue> item) {
      return ContainsKey(item.Key);
    }

    public bool Contains(TKey key) {
      return ContainsKey(key);
    }

    public bool TryGetValue(TKey key, out TValue value) {
      var hashCode = _comparer.GetHashCode(key);
      var bucketIndex = GetBucketIndex(hashCode, _buckets.Length);
      for (var entryIndex = AdjustBucketIndexRead(_buckets[bucketIndex]); entryIndex >= 0;) {
        var entry = _entries[entryIndex];
        if (entry.HashCode == hashCode && _comparer.Equals(key, _getKey(entry.Value))) {
          value = entry.Value;
          return true;
        }
        entryIndex = _entries[entryIndex].NextIndex;
      }

      value = default(TValue);
      return false;
    }

    public TValue GetOrDefault(TKey key) {
      return GetOrDefault(key, default(TValue));
    }

    public TValue GetOrDefault(TKey key, TValue defaultValue) {
      TValue result;
      if (!TryGetValue(key, out result)) {
        return defaultValue;
      }
      return result;
    }

    public TValue GetOrAdd(TKey key, TValue value) {
      var hashCode = _comparer.GetHashCode(key);
      var bucketIndex = GetBucketIndex(hashCode, _buckets.Length);
      for (var entryIndex = AdjustBucketIndexRead(_buckets[bucketIndex]); entryIndex >= 0;) {
        var entry = _entries[entryIndex];
        if (entry.HashCode == hashCode && _comparer.Equals(key, _getKey(entry.Value))) {
          return entry.Value;
        }
        entryIndex = _entries[entryIndex].NextIndex;
      }

      AddEntryWorker(value, bucketIndex, hashCode);
      return value;
    }

    public void UpdateOrAdd(TKey key, TValue value) {
      var hashCode = _comparer.GetHashCode(key);
      var bucketIndex = GetBucketIndex(hashCode, _buckets.Length);
      for (var entryIndex = AdjustBucketIndexRead(_buckets[bucketIndex]); entryIndex >= 0;) {
        var entry = _entries[entryIndex];
        if (entry.HashCode == hashCode && _comparer.Equals(key, _getKey(entry.Value))) {
          _entries[entryIndex].Value = value;
          return;
        }
        entryIndex = _entries[entryIndex].NextIndex;
      }

      AddEntryWorker(value, bucketIndex, hashCode);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
      CopyTo(array, (k, v) => new KeyValuePair<TKey, TValue>(k, v), arrayIndex);
    }

    public void CopyTo<TArray>(TArray[] array, Func<TKey, TValue, TArray> convert, int arrayIndex) {
      var index = arrayIndex;
      foreach (var kvp in this) {
        array[index] = convert(kvp.Key, kvp.Value);
        index++;
      }
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
      var bucketIndex = GetBucketIndex(hashCode, _buckets.Length);
      for (var entryIndex = AdjustBucketIndexRead(_buckets[bucketIndex]); entryIndex >= 0;) {
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

      // We need to actually add the value
      AddEntryWorker(value, bucketIndex, hashCode);
    }

    private void AddEntryWorker(TValue value, int bucketIndex, int hashCode) {
      if (_count >= _buckets.Length) {
        Grow();
        bucketIndex = GetBucketIndex(hashCode, _buckets.Length);
      }

      // Insert at head of list starting at (new) bucket Index
      int newEntryIndex;
      if (_freeListHead >= 0) {
        newEntryIndex = _freeListHead;
        _freeListHead = _entries[_freeListHead].NextFreeIndex;
      } else {
        // Free list is empty, so insertion is at end of list of _entries
        newEntryIndex = _count;
      }

      _entries[newEntryIndex] = new Entry(value, hashCode, AdjustBucketIndexRead(_buckets[bucketIndex]));
      _buckets[bucketIndex] = AdjustBucketIndexWrite(newEntryIndex);
      _count++;
    }

    private void Grow() {
      Invariants.Assert(_count == _entries.Length);
      Invariants.Assert(_count == _buckets.Length);
      Invariants.Assert(_freeListHead== -1);
      var oldEntries = _entries;
      var oldLength = _entries.Length;

      var newLength = HashCode.GetPrime(GrowSize(oldLength));
      var newBuckets = new int[newLength];
      var newEntries = new Entry[newLength];
      Array.Copy(_entries, newEntries, _entries.Length);

      for (var entryIndex = 0; entryIndex < oldEntries.Length; entryIndex++) {
        var oldEntry = oldEntries[entryIndex];
        if (oldEntry.IsValid) {
          var newBucketIndex = GetBucketIndex(oldEntry.HashCode, newLength);
          newEntries[entryIndex].SetNextIndex(AdjustBucketIndexRead(newBuckets[newBucketIndex]));
          newBuckets[newBucketIndex] = AdjustBucketIndexWrite(entryIndex);
        }
      }

      _buckets = newBuckets;
      _entries = newEntries;
    }

    private static int AdjustBucketIndexRead(int index) {
      return index - 1;
    }

    private static int AdjustBucketIndexWrite(int index) {
      return index + 1;
    }

    private static int GetPrimeLength(int capacity) {
      return HashCode.GetPrime(capacity);
    }

    private int GrowSize(int oldLength) {
      return Math.Min(oldLength + 1, _growthPolicy.Grow(oldLength));
    }

    private static int GetBucketIndex(int hashcode, int length) {
      return (hashcode & int.MaxValue) % length;
    }
  }
}