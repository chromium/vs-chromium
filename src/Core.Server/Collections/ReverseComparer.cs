// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Core.Collections {
  public class ReverseComparer<T> : IComparer<T> {
    private readonly IComparer<T> _comparer;

    public ReverseComparer(IComparer<T> comparer) {
      _comparer = comparer;
    }

    public int Compare(T x, T y) {
      return -_comparer.Compare(x, y);
    }
  }
}
