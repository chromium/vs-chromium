// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;

namespace VsChromiumCore.FileNames.PatternMatching {
  public class OpText : BaseOperator, IPrePassWontMatch {
    private readonly string _text;

    public OpText(string text) {
      this._text = text;
    }

    public string Text {
      get {
        return this._text;
      }
    }

    public bool PrePassWontMatch(MatchKind kind, string path, IPathComparer comparer) {
      return path.IndexOf(this._text, 0, path.Length, comparer.Comparison) < 0;
    }

    public override string ToString() {
      return this._text;
    }

    public override int MatchWorker(MatchKind kind, IPathComparer comparer, IList<BaseOperator> operators, int operatorIndex, string path, int pathIndex) {
      var len = this._text.Length;

      if (String.Compare(this._text, 0, path, pathIndex, len, comparer.Comparison) != 0)
        return -1;

      return Match(kind, comparer, operators, operatorIndex + 1, path, pathIndex + len);
    }
  }
}
