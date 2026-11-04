// Copyright 2026 The Chromium Authors. All rights reserved.
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
  /// <summary>
  /// "Tools > Options > VS Chromium > Index Server": the settings of the
  /// indexing server (the equivalent of the "vschromium.cache*" and
  /// "vschromium.trigramFilters" settings of VS Code). The trigram filters and
  /// cache options are the defaults of the projects (each project can override
  /// them from the server dashboard); the cache directory is applied when the
  /// server starts.
  /// </summary>
  [ClassInterface(ClassInterfaceType.AutoDual)]
  [ComVisible(true)]
  public class IndexServerOptions : DialogPage {
    private const string ProjectsCategory = "Projects (defaults)";
    private const string CacheCategory = "Index Cache";

    public IndexServerOptions() {
      ResetValues();
    }

    private void ResetValues() {
      TrigramFiltersEnabled = false;
      CacheEnabled = true;
      CacheDirectory = "";
    }

    public override void ResetSettings() {
      base.ResetSettings();
      ResetValues();
    }

    [Category(ProjectsCategory)]
    [DisplayName("Trigram filters")]
    [Description("Use trigram filters for the projects: much faster selective code searches (e.g. symbol names), at the cost of memory " +
                 "(about 5% of the size of the indexed text). Default of the projects without their own value " +
                 "(set from the server dashboard).")]
    [DefaultValue(false)]
    public bool TrigramFiltersEnabled { get; set; }

    [Category(ProjectsCategory)]
    [DisplayName("Index cache")]
    [Description("Keep a persistent index cache on disk, so that the index of a project is available a few seconds after the " +
                 "server starts. Default of the projects without their own value (set from the server dashboard). " +
                 "Disabling it deletes the cache of these projects.")]
    [DefaultValue(true)]
    public bool CacheEnabled { get; set; }

    [Category(CacheCategory)]
    [DisplayName("Cache directory")]
    [Description("Root directory of the index caches (absolute path on a local disk, a sub-directory per project). Empty for the " +
                 "default (%LOCALAPPDATA%\\VsChromium\\Cache). Applied when the indexing server starts.")]
    [DefaultValue("")]
    public string CacheDirectory { get; set; }

    protected override void OnApply(PageApplyEventArgs e) {
      base.OnApply(e);
      if (e.ApplyBehavior != ApplyKind.Apply) {
        return;
      }
      var componentModel = (IComponentModel)GetService(typeof(SComponentModel));
      if (componentModel == null) {
        Logger.LogError("No component model found in DialogPage.");
        return;
      }
      var bus = componentModel.DefaultExportProvider.GetExportedValue<IDispatchThreadEventBus>();
      bus.PostEvent(EventNames.ToolsOptions.PageApply, this, new EventArgs());
    }
  }
}
