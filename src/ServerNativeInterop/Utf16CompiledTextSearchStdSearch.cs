// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Server.NativeInterop {
  public class Utf16CompiledTextSearchStdSearch : CompiledTextSearchBase {
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

    protected override int SearchBufferSize {
      get { return 0; }
    }

    protected override void CancelSearch(ref NativeMethods.SearchParams searchParams) {
      // Nothing to do.
    }

    protected override void Search(ref NativeMethods.SearchParams searchParams) {
      var start = searchParams.TextStart;
      if (searchParams.MatchStart != IntPtr.Zero) {
        start = searchParams.MatchStart + searchParams.MatchLength * sizeof(char);
      }
      var end = searchParams.TextStart + searchParams.TextLength * sizeof(char);
      var count = Pointers.Offset64(start, end) / sizeof(char);
      var searchHitPtr = NativeMethods.Utf16_Search(
        start,
        count,
        _patternPtr.Pointer,
        _patternLength,
        _searchOptions);
      if (searchHitPtr == IntPtr.Zero) {
        searchParams.MatchStart = IntPtr.Zero;
        searchParams.MatchLength = 0;
      } else {
        searchParams.MatchStart = searchHitPtr;
        searchParams.MatchLength = _patternLength;
      }
    }
  }
}