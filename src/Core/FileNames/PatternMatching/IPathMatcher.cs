// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.FileNames.PatternMatching {
  public interface IPathMatcher {
    bool MatchDirectoryName(RelativePath path, IPathComparer comparer);
    bool MatchFileName(RelativePath path, IPathComparer comparer);
  }
}
