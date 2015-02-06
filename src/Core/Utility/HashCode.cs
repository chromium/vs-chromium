// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Utility {
  public static class HashCode {
    public static int Combine(int h1, int h2) {
      // http://stackoverflow.com/questions/1646807/quick-and-simple-hash-code-combinations
      unchecked {
        int hash = 17;
        hash = hash * 31 + h1;
        hash = hash * 31 + h2;
        return hash;
      }
    }
    public static int Combine(int h1, int h2, int h3) {
      return Combine(Combine(h1, h2), h3);
    }
    public static int Combine(int h1, int h2, int h3, int h4) {
      return Combine(Combine(h1, h2), Combine(h3, h4));
    }
  }
}
