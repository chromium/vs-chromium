// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Files {
  public interface IPathComparer {
    StringComparer StringComparer { get; }

    int Compare(String value1, int startIndex1, String value2, int startIndex2, int length);
    int IndexOf(string value, string searchText, int index, int count);
  }

  public static class PathComparerExtensions {
    public static bool StartsWith(this IPathComparer comparer, string value, string searchText) {
      value = (value ?? string.Empty);
      searchText = (searchText ?? string.Empty);
      return comparer.IndexOf(value, searchText, 0, value.Length) == 0;
    }
    public static bool EndsWith(this IPathComparer comparer, string value, string searchText) {
      value = (value ?? string.Empty);
      searchText = (searchText ?? string.Empty);
      return comparer.IndexOf(value, searchText, 0, value.Length) == (value.Length - searchText.Length);
    }
  }
}
