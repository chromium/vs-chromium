// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Threading;

namespace VsChromium.Core.Utility {
  /// <summary>
  /// Multi-thread safe helper class to limit the number of occurences of some operation
  /// to a given number.
  /// </summary>
  public class BoundedOperationLimiter {
    private readonly int _maxCount;
    private int _count;

    public BoundedOperationLimiter(int maxCount) {
      _maxCount = maxCount;
    }

    public Result Proceed() {
      // Note: The condition is unsafe from a pure concurrency point of view,
      //       but is ok in this case because the field is incrementally increasing.
      //       This is just an optimization to avoid an Interlocked call.
      if (_count > _maxCount) {
        return Result.NoMore;
      }

      var result = Interlocked.Increment(ref _count);
      if (result < _maxCount) {
        return Result.Yes;
      }
      else if (result == _maxCount) {
        return Result.YesAndLast;
      } else {
        return Result.NoMore;
      }
    }

    public enum Result {
      Yes,
      YesAndLast,
      NoMore
    }
  }
}