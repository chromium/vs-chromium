// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromiumCore.FileNames.PatternMatching {
  public class OpNoMatch : BaseOperator, IPrePassWontMatch {
    public bool PrePassWontMatch(MatchKind kind, string path, IPathComparer comparer) {
      return true;
    }

    public override int MatchWorker(MatchKind kind, IPathComparer comparer, IList<BaseOperator> operators, int operatorIndex, string path, int pathIndex) {
      return -1;
    }

    public override string ToString() {
      return "<no match>";
    }
  }
}
