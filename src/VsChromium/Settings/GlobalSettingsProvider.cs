// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Reflection;
using VsChromium.Core.Utility;
using VsChromium.Package;
using VsChromium.ToolsOptions;

namespace VsChromium.Settings {
  [Export(typeof(IGlobalSettingsProvider))]
  public class GlobalSettingsProvider : IGlobalSettingsProvider {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;
    private readonly IEventBus _eventBus;
    private readonly Lazy<GlobalSettings> _globalSettings;

    [ImportingConstructor]
    public GlobalSettingsProvider(IVisualStudioPackageProvider visualStudioPackageProvider, IEventBus eventBus) {
      _visualStudioPackageProvider = visualStudioPackageProvider;
      _eventBus = eventBus;
      _globalSettings = new Lazy<GlobalSettings>(CreateGlobalSettings);
    }

    private GlobalSettings CreateGlobalSettings() {
      var result = new GlobalSettings();
      SetGlobalSettingsProperties(result);

      // Ensure changes to the GlobalSettings object are save to the VS Settings.
      result.PropertyChanged += GlobalSettings_OnPropertyChanged;

      // Ensure changes to the VS Settings are reflected to the GlobalSettings object.
      _eventBus.RegisterHandler("GlobalSettingsChanged", GlobalSettingsChangedHandler);

      return result;
    }

    public GlobalSettings GlobalSettings {
      get {
        return _globalSettings.Value;
      }
    }

    private void SetGlobalSettingsProperties(GlobalSettings globalSettings) {
      var page = _visualStudioPackageProvider.Package.GetToolsOptionsPage<GeneralOptions>();

      globalSettings.SearchFileNamesMaxResults = InRange(page.SearchFileNamesMaxEntries, 1000, 1000 * 1000);
      globalSettings.FindInFilesMaxEntries = InRange(page.FindInFilesMaxEntries, 1000, 1000 * 1000);
      globalSettings.MaxTextExtractLength = InRange(page.MaxTextExtractLength, 10, 1024);
      globalSettings.AutoSearchDelayMsec = InRange(page.AutoSearchDelayMsec, 0, int.MaxValue);
      globalSettings.EnableVsChromiumProjects = page.EnableVsChromiumProjects;
      globalSettings.SearchMatchCase = page.SearchMatchCase;
      globalSettings.SearchMatchWholeWord = page.SearchMatchWholeWord;
      globalSettings.SearchUseRegEx = page.SearchUseRegEx;
      globalSettings.SearchIncludeSymLinks = page.SearchIncludeSymLinks;
    }

    private void GlobalSettingsChangedHandler(object sender, EventArgs eventArgs) {
      SetGlobalSettingsProperties(_globalSettings.Value);
    }

    private void GlobalSettings_OnPropertyChanged(object sender, PropertyChangedEventArgs args) {
      var temp = (GlobalSettings)sender;
      var page = _visualStudioPackageProvider.Package.GetToolsOptionsPage<GeneralOptions>();

      page.SearchFileNamesMaxEntries = temp.SearchFileNamesMaxResults;
      page.FindInFilesMaxEntries = temp.FindInFilesMaxEntries;
      page.MaxTextExtractLength = temp.MaxTextExtractLength;
      page.AutoSearchDelayMsec = temp.AutoSearchDelayMsec;
      page.EnableVsChromiumProjects = temp.EnableVsChromiumProjects;
      page.SearchMatchCase = temp.SearchMatchCase;
      page.SearchMatchWholeWord = temp.SearchMatchWholeWord;
      page.SearchUseRegEx = temp.SearchUseRegEx;
      page.SearchIncludeSymLinks = temp.SearchIncludeSymLinks;

      page.SaveSettingsToStorage();
    }

    private static int InRange(int value, int min, int max) {
      if (value < min)
        return min;
      if (value > max)
        return max;
      return value;
    }
  }
}