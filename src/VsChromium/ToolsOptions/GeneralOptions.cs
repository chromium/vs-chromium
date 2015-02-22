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
  public class GeneralOptions : DialogPage {
    private const string CodeSearchUserInterfaceCategory = "Code Search User Interface";
    private const string CodeSearchOptionsCategory = "Code Search Options";

    public GeneralOptions() {
      SetDefaults();
    }

    private void SetDefaults() {
      FindInFilesMaxEntries = 10 * 1000;
      SearchFileNamesMaxEntries = 2 * 1000;
      MaxTextExtractLength = 120;
      AutoSearchDelayMsec = 20;

      SearchIncludeSymLinks = true;

      EnableVsChromiumProjects = true;
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

    [Category(CodeSearchUserInterfaceCategory)]
    [DisplayName("Maximum number of results for Find in Files")]
    [Description("Limit the number of entries returned when searching for text in files. Higher values may slow down the User Interface.")]
    public int FindInFilesMaxEntries { get; set; }

    [Category(CodeSearchUserInterfaceCategory)]
    [DisplayName("Maximum number of results for Find File Paths")]
    [Description("Limit the numer of entries returned when searching for file names. Higher values may slow down the User Interface.")]
    public int SearchFileNamesMaxEntries { get; set; }

    [Category(CodeSearchUserInterfaceCategory)]
    [DisplayName("Maximum number of characters in text extracts")]
    [Description("Limit the numer of characters displayed per text line in search results. Higher values may slow down the User Interface.")]
    public int MaxTextExtractLength { get; set; }

    [Category(CodeSearchUserInterfaceCategory)]
    [DisplayName("Auto Search delay (milliseconds)")]
    [Description("Time interval to wait after user input before displaying search results. Lower value may slow down the User Interface. Higher values may render the User Interface less responsive.")]
    public int AutoSearchDelayMsec { get; set; }

    [Category(CodeSearchOptionsCategory)]
    [DisplayName("Match case")]
    [Description("Search are case sensitive by default.")]
    public bool SearchMatchCase { get; set; }

    [Category(CodeSearchOptionsCategory)]
    [DisplayName("Match whole word")]
    [Description("Match whole word by default.")]
    public bool SearchMatchWholeWord { get; set; }

    [Category(CodeSearchOptionsCategory)]
    [DisplayName("Use Regular Expression")]
    [Description("Search patterns are regular expression by default.")]
    public bool SearchUseRegEx { get; set; }

    [Category(CodeSearchOptionsCategory)]
    [DisplayName("Search through Symbolic Links")]
    [Description("Search looks at files in symbolic link by default.")]
    public bool SearchIncludeSymLinks { get; set; }

    [Category("Solution Explorer Integration")]
    [DisplayName("Enable \"VS Chromium Projects\" entry")]
    [Description("Show the list of \"VS Chromium Projects\" and their files in Solution Explorer")]
    public bool EnableVsChromiumProjects { get; set; }
  }
}
