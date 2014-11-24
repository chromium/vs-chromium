// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Files {
  public class PathComparerRegistry {
    private static readonly IPathComparer _caseInsensitive = new PathComparer(PathComparisonOption.CaseInsensitive);
    private static readonly IPathComparer _caseSensitive = new PathComparer(PathComparisonOption.CaseSensitive);

    public static IPathComparer Default {
      get {
        // TODO(rpaquay): Maybe one day we will support *nix file systems.
        return _caseInsensitive;
      }
    }

    public static IPathComparer CaseInsensitive {
      get { return _caseInsensitive; }
    }

    public static IPathComparer CaseSensitive {
      get { return _caseSensitive; }
    }
  }
}