// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text;

namespace VsChromium.Core.Collections {
  public class StringBuilderPool : ObjectPool<StringBuilder> {
    public StringBuilderPool() : base(CreateStringBuilder, RecycleStringBuilder) {
    }

    private static StringBuilder CreateStringBuilder() {
      return new StringBuilder();
    }

    private static void RecycleStringBuilder(StringBuilder sb) {
      sb.Clear();
      // Shrink to avoid using too much memory in the pool
      if (sb.Capacity > 1024) {
        sb.Capacity = 1024;
      }
    }
  }
}