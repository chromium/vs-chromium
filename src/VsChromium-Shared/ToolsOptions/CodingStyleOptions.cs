// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
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

    public CodingStyleOptions(JoinableTaskContext jtc) : base(jtc) {
      SetDefaults();
    }

    private void SetDefaults() {
      // Default.
      AccessorIndent = false;
      ElseIfOnNewLine = false;
      EndOfLineCharacter = false;
      LongLine = false;
      OpenBraceAfterNewLine = false;
      SpaceAfterForKeyword = false;
      TabCharacter = false;
      TrailingSpace = false;
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

      var bus = componentModel.DefaultExportProvider.GetExportedValue<IDispatchThreadEventBus>();
      bus.PostEvent(EventNames.ToolsOptions.PageApply, this, new EventArgs());
    }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check class access modifiers indentation")]
    [Description("Check class access modifiers indentation is 1 character less than memebers")]
    [DefaultValue(false)]
    public bool AccessorIndent { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check for \"else if\" at start of line")]
    [Description("Check for \"else if\" at start of line")]
    [DefaultValue(false)]
    public bool ElseIfOnNewLine { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check line endings are Unix style")]
    [Description("Check line endings are Unix style ('\n' character only)")]
    [DefaultValue(false)]
    public bool EndOfLineCharacter { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check lines are 80 characters or less")]
    [Description("Check lines are 80 characters or less")]
    [DefaultValue(false)]
    public bool LongLine { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check for open curly brace at start of line")]
    [Description("Check for open curly brace at start of line")]
    [DefaultValue(false)]
    public bool OpenBraceAfterNewLine { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check for space after \"for\" keyword")]
    [Description("Check for space after \"for\" keyword")]
    [DefaultValue(false)]
    public bool SpaceAfterForKeyword { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check for tab characters")]
    [Description("Check for tab characters")]
    [DefaultValue(false)]
    public bool TabCharacter { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check for trailing spaces at end of line")]
    [Description("Check for trailing spaces at end of line")]
    [DefaultValue(false)]
    public bool TrailingSpace { get; set; }
  }
}
