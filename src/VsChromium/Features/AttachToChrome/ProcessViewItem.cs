// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using VsChromium.Core.Win32.Processes;
using VsChromium.Core.Processes;
using VsChromium.Core.Chromium;

namespace VsChromium.Features.AttachToChrome {
  class ProcessViewItem : ListViewItem {
    public string Exe;
    public int SessionId;
    public string Title;
    public string DisplayCmdLine;
    public ChromiumProcess Process;
  }
}
