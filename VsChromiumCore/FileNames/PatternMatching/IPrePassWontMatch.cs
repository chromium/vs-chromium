// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromiumCore.FileNames.PatternMatching {
  public interface IPrePassWontMatch {
    /// <summary>
    /// Returns "true" if this operator can globally say that "path" can't match the operator.
    /// </summary>
    bool PrePassWontMatch(MatchKind kind, string path, IPathComparer comparer);
  }
}
