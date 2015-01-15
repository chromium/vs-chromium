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
    private readonly int _searchBufferSize;

    public AsciiStringSearchNative(NativeMethods.SearchAlgorithmKind kind, string pattern, NativeMethods.SearchOptions searchOptions) {
      _patternHandle = new SafeHGlobalHandle(Marshal.StringToHGlobalAnsi(pattern));
      _patternLength = pattern.Length;

      _handle = CreateSearchHandle(kind, _patternHandle, _patternLength, searchOptions);
      _searchBufferSize = NativeMethods.AsciiSearchAlgorithm_GetSearchBufferSize(_handle);
    }

    private static unsafe SafeSearchHandle CreateSearchHandle(
      NativeMethods.SearchAlgorithmKind kind,
      SafeHGlobalHandle patternHandle,
      int patternLength,
      NativeMethods.SearchOptions searchOptions) {

      NativeMethods.SearchCreateResult createResult;
      var result = NativeMethods.AsciiSearchAlgorithm_Create(
        kind,
        patternHandle.Pointer,
        patternLength,
        searchOptions,
        out createResult);

      if (createResult.HResult < 0) {
        var message = Marshal.PtrToStringAnsi(new IntPtr(createResult.ErrorMessage));
        throw new ArgumentException(message);
      }

      return result;
    }

    public override int PatternLength {
      get { return _patternLength; }
    }

    public override int SearchBufferSize {
      get { return _searchBufferSize; }
    }

    public override void Search(ref NativeMethods.SearchParams searchParams) {
      NativeMethods.AsciiSearchAlgorithm_Search(_handle, ref searchParams);
    }

    public override void Dispose() {
      _handle.Dispose();
      _patternHandle.Dispose();
    }
  }
}
