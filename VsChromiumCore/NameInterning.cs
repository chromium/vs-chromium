// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Concurrent;

namespace VsChromium.Core {
  /// <summary>
  /// Similar to "string.intern" except it is slightly more efficient (at the cost
  /// of slightly more memory).
  /// </summary>
  public static class NameInterning {
    private static readonly ConcurrentDictionary<string, string> _strings = new ConcurrentDictionary<string, string>();

    public static string Get(string value) {
      return _strings.GetOrAdd(value, value);
    }
  }
}
