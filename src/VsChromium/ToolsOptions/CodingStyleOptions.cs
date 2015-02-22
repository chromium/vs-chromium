// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;

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

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check class accessor indentation")]
    [Description("Check class accessor indentation.")]
    public bool AccessorIndent { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check \"else\" on same line as close brace")]
    [Description("Check \"else\" on same line as close brace")]
    public bool ElseIfOnNewLine { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check line endings are Unix style")]
    [Description("Check line endings are Unix style")]
    public bool EndOfLineCharacter { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check lines are 80 characters or less")]
    [Description("Check lines are 80 characters or less")]
    public bool LongLine { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check open braces are not on new line")]
    [Description("Check open braces are not on new line")]
    public bool OpenBraceAfterNewLine { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check no space after \"for\" keyword")]
    [Description("Check no space after \"for\" keyword")]
    public bool SpaceAfterForKeyword { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check no tab characters")]
    [Description("Check no tab characters")]
    public bool TabCharacter { get; set; }

    [Category(ChromiumCodingStyleCategory)]
    [DisplayName("Check for trailing spaces at end of line")]
    [Description("Check for trailing spaces at end of line")]
    public bool TrailingSpace { get; set; }
  }
}
