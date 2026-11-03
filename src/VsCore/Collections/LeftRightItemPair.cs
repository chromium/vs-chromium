// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Collections {
  public struct LeftRightItemPair<T> {
    private readonly T _leftItem;
    private readonly T _rigthtItem;

    public LeftRightItemPair(T leftItem, T rigthtItem) {
      _leftItem = leftItem;
      _rigthtItem = rigthtItem;
    }

    public T LeftItem {
      get { return _leftItem; }
    }

    public T RigthtItem {
      get { return _rigthtItem; }
    }
  }
}