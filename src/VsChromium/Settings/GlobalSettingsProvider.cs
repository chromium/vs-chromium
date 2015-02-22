// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Threading;
using VsChromium.Core.Utility;
using VsChromium.Package;
using VsChromium.Threads;
using VsChromium.ToolsOptions;

namespace VsChromium.Settings {
  [Export(typeof(IGlobalSettingsProvider))]
  public class GlobalSettingsProvider : IGlobalSettingsProvider {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;
    private readonly IEventBus _eventBus;
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;
    private readonly Lazy<GlobalSettings> _globalSettings;
    private readonly int _threadId = Thread.CurrentThread.ManagedThreadId;
    private bool _swallowGlobalSettingsPropertyChangeNotifications;

    [ImportingConstructor]
    public GlobalSettingsProvider(
      IVisualStudioPackageProvider visualStudioPackageProvider,
      IEventBus eventBus,
      ISynchronizationContextProvider synchronizationContextProvider) {
      _visualStudioPackageProvider = visualStudioPackageProvider;
      _eventBus = eventBus;
      _synchronizationContextProvider = synchronizationContextProvider;
      _globalSettings = new Lazy<GlobalSettings>(CreateGlobalSettings);
    }

    private GlobalSettings CreateGlobalSettings() {
      var result = new GlobalSettings();
      CopyOptionsPageToGlobalSettings(result);

      // Ensure changes to the GlobalSettings object are save to the VS Settings.
      result.PropertyChanged += GlobalSettingsPropertyChangedHandler;

      // Ensure changes to the VS Settings are reflected to the GlobalSettings object.
      _eventBus.RegisterHandler("ToolsOptionsPageApply", ToolsOptionsPageApplyHandler);

      return result;
    }

    public GlobalSettings GlobalSettings {
      get {
        return _globalSettings.Value;
      }
    }

    /// <summary>
    /// Copy properties from "Tools|Options" page objects to GlobalSettings.
    /// </summary>
    private void CopyOptionsPageToGlobalSettings(GlobalSettings globalSettings) {
      if (Thread.CurrentThread.ManagedThreadId != _threadId) {
        throw new InvalidOperationException("Global Settings can only be mofidied on the UI thread.");
      }
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

      var page2 = _visualStudioPackageProvider.Package.GetToolsOptionsPage<CodingStyleOptions>();
      ReflectionUtils.CopyDeclaredPublicProperties(page2, "", globalSettings, "CodingStyle", true);
    }

    /// <summary>
    /// Invoked when any property of the "Tools|Options" page is modified.
    /// </summary>
    private void ToolsOptionsPageApplyHandler(object sender, EventArgs eventArgs) {
      if (Thread.CurrentThread.ManagedThreadId != _threadId) {
        throw new InvalidOperationException(
          "Global Settings can only be mofidied on the UI thread.");
      }
      _swallowGlobalSettingsPropertyChangeNotifications = true;
      try {
        CopyOptionsPageToGlobalSettings(_globalSettings.Value);
      } finally {
        _swallowGlobalSettingsPropertyChangeNotifications = false;
      }
    }

    /// <summary>
    /// Invoked when any property of the "GlobalSettings" instance is modified.
    /// </summary>
    private void GlobalSettingsPropertyChangedHandler(object sender, PropertyChangedEventArgs args) {
      if (Thread.CurrentThread.ManagedThreadId != _threadId) {
        throw new InvalidOperationException(
          "Global Settings can only be mofidied on the UI thread.");
      }
      if (_swallowGlobalSettingsPropertyChangeNotifications)
        return;
      var globalSettings = (GlobalSettings)sender;

      var page = _visualStudioPackageProvider.Package.GetToolsOptionsPage<GeneralOptions>();
      page.SearchFileNamesMaxEntries = globalSettings.SearchFileNamesMaxResults;
      page.FindInFilesMaxEntries = globalSettings.FindInFilesMaxEntries;
      page.MaxTextExtractLength = globalSettings.MaxTextExtractLength;
      page.AutoSearchDelayMsec = globalSettings.AutoSearchDelayMsec;
      page.EnableVsChromiumProjects = globalSettings.EnableVsChromiumProjects;
      page.SearchMatchCase = globalSettings.SearchMatchCase;
      page.SearchMatchWholeWord = globalSettings.SearchMatchWholeWord;
      page.SearchUseRegEx = globalSettings.SearchUseRegEx;
      page.SearchIncludeSymLinks = globalSettings.SearchIncludeSymLinks;
      page.SaveSettingsToStorage();

      var page2 = _visualStudioPackageProvider.Package.GetToolsOptionsPage<CodingStyleOptions>();
      ReflectionUtils.CopyDeclaredPublicProperties(globalSettings, "CodingStyle", page2, "", false);
      page2.SaveSettingsToStorage();
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