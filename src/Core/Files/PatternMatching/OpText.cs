// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;

namespace VsChromium.Core.Files.PatternMatching {
  public class OpText : BaseOperator, IPrePassWontMatch {
    private readonly string _text;

    public OpText(string text) {
      _text = text;
    }

    public string Text { get { return _text; } }

    public bool PrePassWontMatch(MatchKind kind, string path, IPathComparer comparer) {
      return comparer.IndexOf(path, _text, 0, path.Length) < 0;
    }

    public override string ToString() {
      return _text;
    }

    public override int MatchWorker(MatchKind kind, IPathComparer comparer, IList<BaseOperator> operators, int operatorIndex, string path, int pathIndex) {
      var len = _text.Length;

      if (comparer.Compare(_text, 0, path, pathIndex, len) != 0)
        return -1;

      return Match(kind, comparer, operators, operatorIndex + 1, path, pathIndex + len);
    }
  }
}
