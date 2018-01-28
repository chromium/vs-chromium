// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Collections {
  public class LinearGrowthPolicy : ICollectionGrowthPolicy {
    private readonly int _step;

    public LinearGrowthPolicy(int step) {
      _step = step;
    }
    public int Grow(int currentSize) {
      return currentSize + _step;
    }
  }
}