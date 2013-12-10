// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromiumCore.Win32.Memory {
  public class SafeProcessHeapHandle : SafeHeapHandle {
    protected override bool ReleaseHandle() {
      // Don't free the process heap handle!
      return true;
    }
  }
}
