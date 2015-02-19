// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Core.Collections {
  /// <summary>
  /// The result of computing array differences.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class ArrayDiffsResult<T> {
    /// <summary>
    /// Elements present only in the first array
    /// </summary>
    public IList<T> LeftOnlyItems { get; set; }
    /// <summary>
    /// Elements present only in the second array
    /// </summary>
    public IList<T> RightOnlyItems { get; set; }
    /// <summary>
    /// Elements present in both arrays
    /// </summary>
    public IList<LeftRightItemPair<T>> CommonItems { get; set; }
  }
}