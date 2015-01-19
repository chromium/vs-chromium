using System;
using System.Linq;

namespace VsChromium.Core.Collections {
  /// <summary>
  /// Bit array implementation using multiple underlying locked bit arrays
  /// to try to minimize lock contention. The array size is determined at
  /// construction time.
  /// </summary>
  public class PartitionedBitArray : IBitArray {
    private readonly ConcurrentBitArray[] _arrays;
    private readonly long _partitionSize;

    public PartitionedBitArray(long count, int partitionCount) {
      if (partitionCount <= 0)
        throw new ArgumentOutOfRangeException();

      _partitionSize = (count + partitionCount - 1) / partitionCount;
      _arrays = Enumerable.Range(0, partitionCount)
        .Select(i => new ConcurrentBitArray(_partitionSize))
        .ToArray();
    }

    public long Count {
      get { return _arrays.Aggregate(0L, (c, x) => c + x.Count); }
    }

    public void Set(long index, bool value) {
      var slot = index / _partitionSize;
      var bit = index % _partitionSize;
      _arrays[slot].Set(bit, value);
    }

    public bool Get(long index) {
      var slot = index / _partitionSize;
      var bit = index % _partitionSize;
      return _arrays[slot].Get(bit);
    }
  }
}