// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromiumCore.FileNames {
  public class CaseInsensitivePathComparer : IPathComparer {
    private static readonly CaseInsensitivePathComparer _theInstance = new CaseInsensitivePathComparer();

    public static IPathComparer Instance { get { return _theInstance; } }

    public StringComparer Comparer { get { return StringComparer.OrdinalIgnoreCase; } }

    public StringComparison Comparison { get { return StringComparison.OrdinalIgnoreCase; } }
  }
}
