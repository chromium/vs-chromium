// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using VsChromiumCore.Win32.Memory;

namespace VsChromiumServer.VsChromiumNative {
  public class AsciiStringSearchNative : AsciiStringSearchAlgorithm {
    private readonly SafeSearchHandle _handle;
    private readonly SafeHGlobalHandle _patternHandle;
    private readonly int _patternLength;

    public AsciiStringSearchNative(NativeMethods.SearchAlgorithmKind kind, string pattern, NativeMethods.SearchOptions searchOptions) {
      this._patternHandle = new SafeHGlobalHandle(Marshal.StringToHGlobalAnsi(pattern));
      this._handle = NativeMethods.AsciiSearchAlgorithm_Create(kind, this._patternHandle.Pointer, pattern.Length, searchOptions);
      this._patternLength = pattern.Length;
    }

    public override int PatternLength {
      get {
        return this._patternLength;
      }
    }

    public override IntPtr Search(IntPtr text, int textLen) {
      return NativeMethods.AsciiSearchAlgorithm_Search(this._handle, text, textLen);
    }

    public override void Dispose() {
      this._handle.Dispose();
      this._patternHandle.Dispose();
    }
  }
}
