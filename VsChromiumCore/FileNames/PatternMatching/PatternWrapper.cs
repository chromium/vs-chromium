// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromiumCore.FileNames.PatternMatching {
  /// <summary>
  /// Wraps a sub-string of a pattern value.
  /// </summary>
  public class PatternWrapper {
    private readonly string _patternText;
    private int _index;
    private int _remaining;

    public PatternWrapper(string patternText) {
      this._patternText = patternText;
      this._index = 0;
      this._remaining = patternText.Length;
    }

    public int Index {
      get {
        return this._index;
      }
    }

    public int Remaining {
      get {
        return this._remaining;
      }
    }

    public bool IsEmpty {
      get {
        return this._remaining == 0;
      }
    }

    public char Last {
      get {
        return this._patternText[this._index + this._remaining - 1];
      }
    }

    public char First {
      get {
        return this._patternText[this._index];
      }
    }

    public void RemoveLast() {
      this._remaining--;
    }

    public void Skip(int i) {
      this._index += i;
      this._remaining -= i;
    }

    public string Take(int i) {
      var result = this._patternText.Substring(this._index, i);
      this._index += i;
      this._remaining -= i;
      return result;
    }

    public bool StartsWith(string value) {
      return this._patternText.IndexOf(value, this._index, this._remaining, SystemPathComparer.Instance.Comparison) == this._index;
    }

    public int IndexOf(string value) {
      var result = this._patternText.IndexOf(value, this._index, this._remaining, SystemPathComparer.Instance.Comparison);
      if (result < this._index)
        return -1;
      return result;
    }
  }
}
