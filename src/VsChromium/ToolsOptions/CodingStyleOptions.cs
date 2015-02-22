// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using VsChromium.Core.Logging;
using VsChromium.Package;

namespace VsChromium.ToolsOptions {
  [ClassInterface(ClassInterfaceType.AutoDual)]
  //[CLSCompliant(false)]
  [ComVisible(true)]
  public class CodingStyleOptions : DialogPage {
    private const string ChromiumCodingStyleCategory = "Chromium Coding Style";

    public CodingStyleOptions() {
      SetDefaults();
    }

    private void SetDefaults() {
      // Default.
      AccessorIndent = true;
      ElseIfOnNewLine = true;
      EndOfLineCharacter = true;
      LongLine = true;
      OpenBraceAfterNewLine = true;
      SpaceAfterForKeyword = true;
      TabCharacter = true;
      TrailingSpace = true;
    }

    public override void ResetSettings() {
      base.ResetSettings();
      SetDefaults();
    }

    protected override void OnApply(PageApplyEventArgs e) {
      base.OnApply(e);

      if (e.ApplyBehavior != ApplyKind.Apply)
        return;

      var componentModel = (IComponentModel)this.GetService(typeof(SComponentModel));
      if (componentModel == null) {
        Logger.LogError("No component model found in DialogPage.");
        return;
      }

      var bus = componentModel.DefaultExportProvider.GetExportedValue<IEventBus>();
      bus.Fire("ToolsOptionsPageApply", this, new EventArgs());
    }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check class access modifiers indentation")]
    [Description("Check class access modifiers indentation is 1 character less than memebers")]
    public bool AccessorIndent { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check for \"else if\" at start of line")]
    [Description("Check for \"else if\" at start of line")]
    public bool ElseIfOnNewLine { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check line endings are Unix style")]
    [Description("Check line endings are Unix style ('\n' character only)")]
    public bool EndOfLineCharacter { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check lines are 80 characters or less")]
    [Description("Check lines are 80 characters or less")]
    public bool LongLine { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check for open curly brace at start of line")]
    [Description("Check for open curly brace at start of line")]
    public bool OpenBraceAfterNewLine { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check for space after \"for\" keyword")]
    [Description("Check for space after \"for\" keyword")]
    public bool SpaceAfterForKeyword { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check for tab characters")]
    [Description("Check for tab characters")]
    public bool TabCharacter { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check for trailing spaces at end of line")]
    [Description("Check for trailing spaces at end of line")]
    public bool TrailingSpace { get; set; }
  }
}
