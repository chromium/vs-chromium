// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromiumCore.Win32.Memory {
  public enum HeapFlags : uint {
    Default,
    HeapZeroMemory = 0x00000008,
    HeapGenerateExceptions = 0x00000004,
    HeapNoSerialize = 0x00000001,
  }
}
