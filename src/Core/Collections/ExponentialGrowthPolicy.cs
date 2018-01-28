// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Collections {
  public class ExponentialGrowthPolicy : ICollectionGrowthPolicy {
    public static ExponentialGrowthPolicy Default = new ExponentialGrowthPolicy(2.0);

    private readonly double _factor;

    public ExponentialGrowthPolicy(double factor) {
      Invariants.CheckArgument(factor > 1, nameof(factor), "Factor must be greater than 1");
      _factor = factor;
    }

    public int Grow(int currentSize) {
      return (int)Math.Round(currentSize * _factor);
    }
  }
}