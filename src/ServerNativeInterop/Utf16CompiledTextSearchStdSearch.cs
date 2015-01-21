// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Server.NativeInterop {
  public class Utf16CompiledTextSearchStdSearch : Utf16CompiledTextSearch {
    private readonly SafeHGlobalHandle _patternPtr;
    private readonly int _patternLength;
    private readonly NativeMethods.SearchOptions _searchOptions;

    public Utf16CompiledTextSearchStdSearch(string pattern, NativeMethods.SearchOptions searchOptions) {
      _patternPtr = new SafeHGlobalHandle(Marshal.StringToHGlobalUni(pattern));
      _patternLength = pattern.Length;
      _searchOptions = searchOptions;
    }

    public override void Dispose() {
      _patternPtr.Dispose();
      base.Dispose();
    }

    public override TextFragment Search(TextFragment textFragment) {
      var searchHitPtr = NativeMethods.Utf16_Search(
        textFragment.FragmentStart,
        textFragment.CharacterCount,
        _patternPtr.Pointer,
        _patternLength,
        _searchOptions);
      if (searchHitPtr == IntPtr.Zero)
        return TextFragment.Null;
      return textFragment.Sub(searchHitPtr, _patternLength);
    }

    public override int PatternLength {
      get { return _patternLength; }
    }
  }
}