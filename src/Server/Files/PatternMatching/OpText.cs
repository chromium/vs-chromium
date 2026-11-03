// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Files.PatternMatching {
  public class OpText : BaseOperator, IPrePassWontMatch {
    private readonly string _text;

    public OpText(string text) {
      _text = text;
    }

    public string Text { get { return _text; } }

    public bool PrePassWontMatch(MatchKind kind, ReadOnlySpan<char> path, IPathComparer comparer) {
      return path.IndexOf(_text.AsSpan(), comparer.Comparison) < 0;
    }

    public override string ToString() {
      return _text;
    }

    public override int MatchWorker(MatchKind kind, IPathComparer comparer, BaseOperator[] operators, int operatorIndex, ReadOnlySpan<char> path, int pathIndex) {
      var len = _text.Length;
      if (path.Length - pathIndex < len || !path.Slice(pathIndex, len).Equals(_text.AsSpan(), comparer.Comparison))
        return -1;

      return Match(kind, comparer, operators, operatorIndex + 1, path, pathIndex + len);
    }
  }
}
