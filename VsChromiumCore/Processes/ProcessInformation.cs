// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromiumCore.Win32.Processes;

namespace VsChromiumCore.Processes {
  public class ProcessInformation : IDisposable {
    public SafeProcessHandle ProcessHandle { get; set; }
    public int ProcessId { get; set; }

    public void Dispose() {
      if (ProcessHandle != null)
        ProcessHandle.Dispose();
    }
  }
}