// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Files.PatternMatching {
  public class OpIsNoMatch : BaseOperator, IPrePassWontMatch {
    public bool PrePassWontMatch(MatchKind kind, ReadOnlySpan<char> path, IPathComparer comparer) {
      return true;
    }

    public override int MatchWorker(MatchKind kind, IPathComparer comparer, BaseOperator[] operators, int operatorIndex, ReadOnlySpan<char> path, int pathIndex) {
      return -1;
    }

    public override string ToString() {
      return "<no match>";
    }
  }
}
