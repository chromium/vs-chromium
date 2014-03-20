// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.FileNames {
  /// <summary>
  /// String comparer used for comparing path strings in the pattern matching namespace.
  /// Note that we use a case *sensitive* comparision because we try to follow the syntax
  /// of ".gitignore" files, which are case sensitive.
  /// </summary>
  public class CaseSensitivePathComparer : IPathComparer {
    private static readonly CaseSensitivePathComparer _theInstance = new CaseSensitivePathComparer();

    public static IPathComparer Instance { get { return _theInstance; } }

    public StringComparer Comparer { get { return StringComparer.Ordinal; } }

    public StringComparison Comparison { get { return StringComparison.Ordinal; } }
  }
}
