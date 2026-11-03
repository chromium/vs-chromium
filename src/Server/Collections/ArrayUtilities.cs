// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Collections {
  public static class ArrayUtilities {
    public static class EmptyList<T> {
      public static readonly IList<T> Instance = new List<T>().ToReadOnlyCollection();
    }

    public static void RemoveDuplicates<T>(IList<T> sortedList, Func<T, T, bool> comparer) {
      if (sortedList.Count <= 1)
        return;

      var previousItem = sortedList[0];
      for (var i = 1; i < sortedList.Count; ) {
        var item = sortedList[i];
        if (comparer(previousItem, item)) {
          sortedList.RemoveAt(i);
          continue;
        }
        previousItem = item;
        i++;
      }
    }


  }
}
