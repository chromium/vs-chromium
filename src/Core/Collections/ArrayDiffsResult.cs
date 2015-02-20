// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Core.Collections {
  /// <summary>
  /// The result of computing array differences.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public struct ArrayDiffsResult<T> {
    private readonly IList<T> _leftOnlyItems;
    private readonly IList<T> _rightOnlyItems;
    private readonly IList<LeftRightItemPair<T>> _commonItems;

    public ArrayDiffsResult(IList<T> leftOnlyItems, IList<T> rightOnlyItems, IList<LeftRightItemPair<T>> commonItems) {
      _leftOnlyItems = leftOnlyItems;
      _rightOnlyItems = rightOnlyItems;
      _commonItems = commonItems;
    }

    /// <summary>
    /// Elements present only in the first array
    /// </summary>
    public IList<T> LeftOnlyItems { get { return _leftOnlyItems; } }

    /// <summary>
    /// Elements present only in the second array
    /// </summary>
    public IList<T> RightOnlyItems { get { return _rightOnlyItems; } }

    /// <summary>
    /// Elements present in both arrays
    /// </summary>
    public IList<LeftRightItemPair<T>> CommonItems { get { return _commonItems; } }
  }
}