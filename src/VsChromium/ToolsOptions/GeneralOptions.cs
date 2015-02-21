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
    public GeneralOptions() {
      SetDefaults();
    }

    private void SetDefaults() {
      // Default.
      EnableVsChromiumProjects = true;
      FindInFilesMaxEntries = 10 * 1000;
      SearchFileNamesMaxEntries = 2 * 1000;
      MaxTextExtractLength = 120;
      AutoSearchDelayMsec = 20;
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
      bus.Fire("GlobalSettingsChanged", this, new EventArgs());
    }

    [Category("Projects")]
    [DisplayName("Enable Solution Explorer integration")]
    [Description("Show the \"VS Chromium Projects\" entry in Solution Explorer")]
    public bool EnableVsChromiumProjects { get; set; }

    [Category("Code Search")]
    [DisplayName("Maximum number of entries when searching text")]
    [Description("Limit the number of entries returned when searching for text in files. Higher values may slow down the User Interface.")]
    public int FindInFilesMaxEntries { get; set; }

    [Category("Code Search")]
    [DisplayName("Maximum number of entries when searching file paths")]
    [Description("Limit the numer of entries returned when searching for file names. Higher values may slow down the User Interface.")]
    public int SearchFileNamesMaxEntries { get; set; }

    [Category("Code Search")]
    [DisplayName("Maximum text extract length")]
    [Description("Limit the numer of characters displayed per text line in search results. Higher values may slow down the User Interface.")]
    public int MaxTextExtractLength { get; set; }

    [Category("Code Search")]
    [DisplayName("Auto Search delay (milliseconds)")]
    [Description("Time interval to wait after user input before displaying search results. Lower value may slow down the User Interface. Higher values may render the User Interface less responsive.")]
    public int AutoSearchDelayMsec { get; set; }
  }
}
