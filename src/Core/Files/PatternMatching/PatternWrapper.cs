// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Files.PatternMatching {
  /// <summary>
  /// Wraps a sub-string of a pattern value.
  /// </summary>
  public class PatternWrapper {
    private readonly string _patternText;
    private int _index;
    private int _remaining;

    public PatternWrapper(string patternText) {
      _patternText = patternText;
      _index = 0;
      _remaining = patternText.Length;
    }

    public int Index { get { return _index; } }

    public int Remaining { get { return _remaining; } }

    public bool IsEmpty { get { return _remaining == 0; } }

    public char Last { get { return _patternText[_index + _remaining - 1]; } }

    public char First { get { return _patternText[_index]; } }

    public void RemoveLast() {
      _remaining--;
    }

    public void Skip(int i) {
      _index += i;
      _remaining -= i;
    }

    public string Take(int i) {
      var result = _patternText.Substring(_index, i);
      _index += i;
      _remaining -= i;
      return result;
    }

    public bool StartsWith(string value) {
      return _patternText.IndexOf(value, _index, _remaining, SystemPathComparer.Instance.Comparison) == _index;
    }

    public int IndexOf(string value) {
      var result = _patternText.IndexOf(value, _index, _remaining, SystemPathComparer.Instance.Comparison);
      if (result < _index)
        return -1;
      return result;
    }
  }
}
