// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace VsChromium.ToolsOptions {
  [ClassInterface(ClassInterfaceType.AutoDual)]
  //[CLSCompliant(false)]
  [ComVisible(true)]
  public class DebuggingOptions : DialogPage {
    public DebuggingOptions() {
      SetDefaults();
    }

    private void SetDefaults() {
      // Default.
      EnableChildDebugging = false;
    }

    public override void ResetSettings() {
      base.ResetSettings();
      SetDefaults();
    }

    [Category("Chrome Debugging")]
    [DisplayName("Enable Child Process Debugging")]
    [Description("Enable child-process debugging for launch/attach scenarios other than those available through the Attach to Chrome dialog.")]
    [DefaultValue(false)]
    public bool EnableChildDebugging { get; set; }
  }
}
