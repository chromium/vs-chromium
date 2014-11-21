// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Files {
  public class CaseInsensitivePathComparer : IPathComparer {
    private readonly CustomPathComparer _pathComparer = new CustomPathComparer(PathComparisonOption.CaseInsensitive);

    public StringComparer Comparer { get { return _pathComparer; } }

    public StringComparison Comparison { get { return StringComparison.OrdinalIgnoreCase; } }
  }
}
