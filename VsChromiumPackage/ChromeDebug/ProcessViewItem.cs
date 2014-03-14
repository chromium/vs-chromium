// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using VsChromiumCore.Win32.Processes;
using VsChromiumCore.Processes;

namespace VsChromiumPackage.ChromeDebug {
  class ProcessViewItem : ListViewItem {
    public ProcessViewItem() {
      Category = ProcessCategory.Other;
      MachineType = MachineType.Unknown;
    }

    public string Exe;
    public int ProcessId;
    public int SessionId;
    public string Title;
    public string DisplayCmdLine;
    public string[] CmdLineArgs;
    public ProcessCategory Category;
    public MachineType MachineType;

    public NtProcess Process;
  }
}
