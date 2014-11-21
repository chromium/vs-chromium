// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Files {
  /// <summary>
  /// String comparer used for comparing path strings in the pattern matching namespace.
  /// Note that we use a case *sensitive* comparision because we try to follow the syntax
  /// of ".gitignore" files, which are case sensitive.
  /// </summary>
  public class CaseSensitivePathComparer : IPathComparer {
    private readonly CustomPathComparer _pathComparer = new CustomPathComparer(PathComparisonOption.CaseSensitive);

    public StringComparer Comparer { get { return _pathComparer; } }

    public StringComparison Comparison { get { return StringComparison.Ordinal; } }
  }
}
