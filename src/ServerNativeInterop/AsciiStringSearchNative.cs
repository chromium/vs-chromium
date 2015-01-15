// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Server.NativeInterop {
  public class AsciiStringSearchNative : AsciiStringSearchAlgorithm {
    private readonly SafeSearchHandle _handle;
    private readonly SafeHGlobalHandle _patternHandle;
    private readonly int _patternLength;

    public AsciiStringSearchNative(NativeMethods.SearchAlgorithmKind kind, string pattern, NativeMethods.SearchOptions searchOptions) {
      _patternHandle = new SafeHGlobalHandle(Marshal.StringToHGlobalAnsi(pattern));
      _handle = NativeMethods.AsciiSearchAlgorithm_Create(kind, _patternHandle.Pointer, pattern.Length, searchOptions);
      _patternLength = pattern.Length;
    }

    public override int PatternLength { get { return _patternLength; } }

    public override void Search(IntPtr textPtr, int textLen, NativeMethods.SearchCallback matchFound) {
      NativeMethods.AsciiSearchAlgorithm_Search(_handle, textPtr, textLen, matchFound);
    }

    public override void Dispose() {
      _handle.Dispose();
      _patternHandle.Dispose();
    }
  }
}
