// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Files {
  public interface IPathComparer {
    StringComparer StringComparer { get; }

    /// <summary>
    /// The comparison of characters (<see cref="StringComparison.Ordinal"/> or <see
    /// cref="StringComparison.OrdinalIgnoreCase"/>), e.g. for span based comparisons.
    /// </summary>
    StringComparison Comparison { get; }

    int IndexOf(string value, string searchText, int index, int count);
  }
}
