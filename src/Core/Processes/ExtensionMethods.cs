// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VsChromium.Core.Processes {
  public static class ExtensionMethods {
    public static Process[] GetChildren(this Process process) {
      List<Process> processes = new List<Process>();
      foreach (Process proc in Process.GetProcesses()) {
        if (proc.Id == process.Id)
          continue;

        NtProcess ntproc = new NtProcess(proc.Id);
        if (!ntproc.IsValid)
          continue;

        if (ntproc.ParentProcessId == process.Id)
          processes.Add(proc);
      }
      return processes.ToArray();
    }

    public static bool IsBeingDebugged(this Process process) {
      return (new NtProcess(process.Id)).IsBeingDebugged;
    }
  }
}
