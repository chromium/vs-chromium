// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;

namespace VsChromiumCore.Win32.Memory {
  [Export(typeof(IHeapAlloc))]
  public class HeapAlloc : IHeapAlloc {
    public SafeHeapBlockHandle Alloc(int size) {
      return HeapAllocStatic.Alloc(size);
    }
  }
}
