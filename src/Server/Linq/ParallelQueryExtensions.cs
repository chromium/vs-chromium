// Copyright 2017 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;

namespace VsChromium.Core.Linq {
  public static class ParallelQueryExtensions {
#if DEBUG
    private const bool DefaultEnableParallelExecution = true;
    //private const bool DefaultEnableParallelExecution = false;
#else
    private const bool DefaultEnableParallelExecution = true;
#endif
    /// <summary>
    /// Enable/Disable parallel execution of queries, used for debugging purposes.
    /// </summary>
    private static bool _enableParallelExecution = DefaultEnableParallelExecution;

    public static ParallelQuery<TSource> AsParallelWrapper<TSource>(this IEnumerable<TSource> source) {
      var result = source.AsParallel();
      if (!_enableParallelExecution) {
        result = result.WithDegreeOfParallelism(1);
      }
      return result;
    }
  }
}