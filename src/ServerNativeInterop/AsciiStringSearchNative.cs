// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Server.NativeInterop {
  public class AsciiStringSearchNative : AsciiStringSearchAlgorithm {
    private readonly NativeMethods.SearchOptions _searchOptions;
    private readonly SafeSearchHandle _handle;
    private readonly SafeHGlobalHandle _patternHandle;
    private readonly int _patternLength;

    public AsciiStringSearchNative(NativeMethods.SearchAlgorithmKind kind, string pattern, NativeMethods.SearchOptions searchOptions) {
      _searchOptions = searchOptions;
      _patternHandle = new SafeHGlobalHandle(Marshal.StringToHGlobalAnsi(pattern));
      _handle = NativeMethods.AsciiSearchAlgorithm_Create(kind, _patternHandle.Pointer, pattern.Length, searchOptions);
      _patternLength = pattern.Length;
    }

    public override int PatternLength { get { return _patternLength; } }

    public override IntPtr Search(IntPtr text, int textLen) {
      return NativeMethods.AsciiSearchAlgorithm_Search(_handle, text, textLen);
    }

    public override void Dispose() {
      _handle.Dispose();
      _patternHandle.Dispose();
    }
  }
}
