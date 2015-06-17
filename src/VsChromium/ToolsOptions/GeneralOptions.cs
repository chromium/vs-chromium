// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;
using VsChromium.Package;

namespace VsChromium.ToolsOptions {
  [ClassInterface(ClassInterfaceType.AutoDual)]
  [ComVisible(true)]
  public class GeneralOptions : DialogPage {
    private Font _textExtractFont;
    private Font _displayFont;
    private Font _pathFont;
    private const string CodeSearchUserInterfaceCategory = "Code Search Interface";
    private const string CodeSearchOptionsCategory = "Code Search Options";
    private const BindingFlags PropertyBindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    public GeneralOptions() {
      SetDefaults();
    }

    private void SetDefaults() {
      var properties = this.GetType().GetProperties(PropertyBindingFlags);
      foreach (var property in properties) {
        property.SetValue(this, GetDefaultValueFromAttribute(property));
      }
    }

    private TProperty GetDefaultValueFromAttribute<TProperty>(Expression<Func<GeneralOptions, TProperty>> propertyLambda) {
      var name = ReflectionUtils.GetPropertyName(this, propertyLambda);
      var p = this.GetType().GetProperty(name, PropertyBindingFlags);
      return (TProperty)GetDefaultValueFromAttribute(p);
    }

    private object GetDefaultValueFromAttribute(PropertyInfo property) {
      foreach (var attr in property.GetCustomAttributes(true)) {
        var dv = attr as DefaultValueAttribute;
        if (dv != null) {
          return dv.Value;
        }
      }
      return ReflectionUtils.GetTypeDefaultValue(property.PropertyType);
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
    [DefaultValue(typeof (Font), "Segoe UI, 12pt")]
    public Font DisplayFont {
      get { return _displayFont; }
      set {
        if (value == null)
          value = GetDefaultValueFromAttribute(x => x.DisplayFont);
        _displayFont = value;
      }
    }

    [Category(CodeSearchUserInterfaceCategory)]
    [DisplayName("Text extracts font")]
    [Description("Font used to display text extracts entries in the Code Search tool window.")]
    [DefaultValue(typeof(Font), "Consolas, 13pt")]
    public Font TextExtractFont {
      get { return _textExtractFont; }
      set {
        if (value == null)
          value = GetDefaultValueFromAttribute(x => x.TextExtractFont);
        _textExtractFont = value;
      }
    }

    [Category(CodeSearchUserInterfaceCategory)]
    [DisplayName("Path font")]
    [Description("Font used to display file system paths entries in the Code Search tool window.")]
    [DefaultValue(typeof(Font), "Segoe UI, 12pt")]
    public Font PathFont {
      get { return _pathFont; }
      set {
        if (value == null)
          value = GetDefaultValueFromAttribute(x => x.PathFont);
        _pathFont = value;
      }
    }

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
