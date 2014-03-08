// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using System.Linq;
using System.Text;

namespace VsChromiumPackage.ChromeDebug {
  class NumericStringComparer : IComparer {
    public int Compare(object x, object y) {
      string sx = (string)x;
      string sy = (string)y;
      int nx, ny;
      if (!int.TryParse(sx, out nx))
        return 0;
      if (!int.TryParse(sy, out ny))
        return 0;

      return nx.CompareTo(ny);
    }
  }
}
