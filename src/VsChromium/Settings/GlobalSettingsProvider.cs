// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Threading;
using VsChromium.Core.Utility;
using VsChromium.Package;
using VsChromium.ToolsOptions;

namespace VsChromium.Settings {
  [Export(typeof(IGlobalSettingsProvider))]
  public class GlobalSettingsProvider : IGlobalSettingsProvider {
    private readonly IToolsOptionsPageProvider _visualStudioPackageProvider;
    private readonly IEventBus _eventBus;
    private readonly Lazy<GlobalSettings> _globalSettings;
    private readonly int _writeThreadId = Thread.CurrentThread.ManagedThreadId;
    private bool _swallowGlobalSettingsPropertyChangeNotifications;

    [ImportingConstructor]
    public GlobalSettingsProvider(
      IToolsOptionsPageProvider visualStudioPackageProvider,
      IEventBus eventBus) {
      _visualStudioPackageProvider = visualStudioPackageProvider;
      _eventBus = eventBus;
      _globalSettings = new Lazy<GlobalSettings>(CreateGlobalSettings);
    }

    public GlobalSettings GlobalSettings {
      get {
        return _globalSettings.Value;
      }
    }

    /// <summary>
    /// Invoked when any property of the "Tools|Options" page is modified.
    /// </summary>
    private void ToolsOptionsPageApplyHandler(object sender, EventArgs eventArgs) {
      CheckOnWriteThread();

      _swallowGlobalSettingsPropertyChangeNotifications = true;
      try {
        CopyOptionsPagesToGlobalSettings(_globalSettings.Value);
      }
      finally {
        _swallowGlobalSettingsPropertyChangeNotifications = false;
      }
    }

    /// <summary>
    /// Invoked when any property of the "GlobalSettings" instance is modified.
    /// </summary>
    private void GlobalSettingsPropertyChangedHandler(object sender, PropertyChangedEventArgs args) {
      CheckOnWriteThread();

      if (_swallowGlobalSettingsPropertyChangeNotifications)
        return;

      var globalSettings = (GlobalSettings)sender;
      CopyGlobalSettingsToOptionPages(globalSettings);
    }

    private GlobalSettings CreateGlobalSettings() {
      var result = new GlobalSettings();
      CopyOptionsPagesToGlobalSettings(result);

      // Ensure changes to the GlobalSettings object are save to the VS Settings.
      result.PropertyChanged += GlobalSettingsPropertyChangedHandler;

      // Ensure changes to the VS Settings are reflected to the GlobalSettings object.
      _eventBus.RegisterHandler("ToolsOptionsPageApply", ToolsOptionsPageApplyHandler);

      return result;
    }

    /// <summary>
    /// Copy properties from "Tools|Options" page objects to <paramref
    /// name="globalSettings"/>.
    /// </summary>
    private void CopyOptionsPagesToGlobalSettings(GlobalSettings globalSettings) {
      CheckOnWriteThread();

      var page = _visualStudioPackageProvider.GetToolsOptionsPage<GeneralOptions>();
      ReflectionUtils.CopyDeclaredPublicProperties(page, "", globalSettings, "", throwOnExtraProperty: true);

      var page2 = _visualStudioPackageProvider.GetToolsOptionsPage<CodingStyleOptions>();
      ReflectionUtils.CopyDeclaredPublicProperties(page2, "", globalSettings, "CodingStyle", throwOnExtraProperty: true);
    }

    /// <summary>
    /// Copy properties from <paramref name="globalSettings"/> to
    /// "Tools|Options" pages objects.
    /// </summary>
    private void CopyGlobalSettingsToOptionPages(GlobalSettings globalSettings) {
      CheckOnWriteThread();

      var page = _visualStudioPackageProvider.GetToolsOptionsPage<GeneralOptions>();
      ReflectionUtils.CopyDeclaredPublicProperties(globalSettings, "", page, "", throwOnExtraProperty: false);
      page.SaveSettingsToStorage();

      var page2 = _visualStudioPackageProvider.GetToolsOptionsPage<CodingStyleOptions>();
      ReflectionUtils.CopyDeclaredPublicProperties(globalSettings, "CodingStyle", page2, "", throwOnExtraProperty: false);
      page2.SaveSettingsToStorage();
    }

    private void CheckOnWriteThread() {
      if (Thread.CurrentThread.ManagedThreadId != _writeThreadId) {
        throw new InvalidOperationException("Global Settings can only be mofidied on the UI thread.");
      }
    }
  }
}