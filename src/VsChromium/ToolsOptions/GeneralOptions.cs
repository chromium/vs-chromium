// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using VsChromium.Core.Logging;
using VsChromium.Package;

namespace VsChromium.ToolsOptions {
  [ClassInterface(ClassInterfaceType.AutoDual)]
  [ComVisible(true)]
  public class GeneralOptions : DialogPage {
    private const string CodeSearchUserInterfaceCategory = "Code Search Interface";
    private const string CodeSearchOptionsCategory = "Code Search Options";

    public GeneralOptions() {
      SetDefaults();
    }

    private void SetDefaults() {
      SearchCodeMaxResults = 10 * 1000;
      SearchFilePathsMaxResults = 2 * 1000;
      MaxTextExtractLength = 120;
      AutoSearchDelayMsec = 20;
      DisplayFont = new Font(new FontFamily("Segoe UI"), 12);
      TextExtractFont = new Font(new FontFamily("Consolas"), 13);
      
      SearchIncludeSymLinks = true;

      EnableSourceExplorerHierarchy = true;
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
    [DisplayName("Maximum number of results for Seach Code")]
    [Description("Limit the number of entries returned when searching for text in files. Higher values may slow down the User Interface.")]
    [DefaultValue(10 * 1000)]
    public int SearchCodeMaxResults { get; set; }

    [Category(CodeSearchUserInterfaceCategory)]
    [DisplayName("Maximum number of results for Search File Paths")]
    [Description("Limit the numer of entries returned when searching for file paths. Higher values may slow down the User Interface.")]
    [DefaultValue(2 * 1000)]
    public int SearchFilePathsMaxResults { get; set; }

    [Category(CodeSearchUserInterfaceCategory)]
    [DisplayName("Maximum number of characters in text extracts")]
    [Description("Limit the numer of characters displayed per text line in search results. Higher values may slow down the User Interface.")]
    [DefaultValue(120)]
    public int MaxTextExtractLength { get; set; }

    [Category(CodeSearchUserInterfaceCategory)]
    [DisplayName("Auto Search delay (milliseconds)")]
    [Description("Time interval to wait after user input before displaying search results. Lower value may slow down the User Interface.")]
    [DefaultValue(20)]
    public int AutoSearchDelayMsec { get; set; }

    [Category(CodeSearchUserInterfaceCategory)]
    [DisplayName("Display font")]
    [Description("Font used to display entries in the Code Search tool window.")]
    [DefaultValue(typeof(Font), "Segoe UI, 12pt")]
    public Font DisplayFont { get; set; }

    [Category(CodeSearchUserInterfaceCategory)]
    [DisplayName("Text extracts font")]
    [Description("Font used to display text extracts entries in the Code Search tool window.")]
    [DefaultValue(typeof(Font), "Consolas, 13pt")]
    public Font TextExtractFont { get; set; }

    [Category(CodeSearchOptionsCategory)]
    [DisplayName("Match case")]
    [Description("Enable the \"Match Case\" option at startup.")]
    [DefaultValue(false)]
    public bool SearchMatchCase { get; set; }

    [Category(CodeSearchOptionsCategory)]
    [DisplayName("Match whole word")]
    [Description("Enable the \"Match whole word\" option at startup.")]
    [DefaultValue(false)]
    public bool SearchMatchWholeWord { get; set; }

    [Category(CodeSearchOptionsCategory)]
    [DisplayName("Use Regular Expression")]
    [Description("Enable the \"Use regular option\" option at startup.")]
    [DefaultValue(false)]
    public bool SearchUseRegEx { get; set; }

    [Category(CodeSearchOptionsCategory)]
    [DisplayName("Search through Symbolic Links")]
    [Description("Enable the \"Include Symbolic Links\" option at startup.")]
    [DefaultValue(true)]
    public bool SearchIncludeSymLinks { get; set; }

    [Category("Solution Explorer Integration")]
    [DisplayName("Enable \"Source Explorer\" entries in Solution Explorer")]
    [Description("Show the list of indexed files and directories in Solution Explorer under one or more \"Source Explorer\" entries")]
    [DefaultValue(true)]
    public bool EnableSourceExplorerHierarchy { get; set; }
  }
}
