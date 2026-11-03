// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Core.Files.PatternMatching {
  public class OpIsDirectoryOnly : BaseOperator {
    public override int MatchWorker(MatchKind kind, IPathComparer comparer, IList<BaseOperator> operators, int operatorIndex, string path, int pathIndex) {
      var result = Match(kind, comparer, operators, operatorIndex + 1, path, pathIndex);
      if (kind == MatchKind.File && result == path.Length)
        return -1;
      return result;
    }

    public override string ToString() {
      return "<dir-only>";
    }
  }
}
