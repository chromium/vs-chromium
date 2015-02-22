// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Reflection;
using VsChromium.Package;
using VsChromium.ToolsOptions;

namespace VsChromium.Settings {
  [Export(typeof(IGlobalSettingsProvider))]
  public class GlobalSettingsProvider : IGlobalSettingsProvider {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;
    private readonly IEventBus _eventBus;

    [ImportingConstructor]
    public GlobalSettingsProvider(IVisualStudioPackageProvider visualStudioPackageProvider, IEventBus eventBus) {
      _visualStudioPackageProvider = visualStudioPackageProvider;
      _eventBus = eventBus;
    }

    public GlobalSettings GlobalSettings {
      get {
        var page = _visualStudioPackageProvider.Package.GetToolsOptionsPage<GeneralOptions>();
        return new GlobalSettings {
          SearchFileNamesMaxResults = InRange(page.SearchFileNamesMaxEntries, 1000, 1000 * 1000),
          SearchTextMaxResults = InRange(page.SearchFileNamesMaxEntries, 1000, 1000 * 1000),
          MaxTextExtractLength = InRange(page.MaxTextExtractLength, 10, 1024),
          AutoSearchDelay = TimeSpan.FromMilliseconds(InRange(page.AutoSearchDelayMsec, 0, int.MaxValue)),
          EnableVsChromiumProjects = page.EnableVsChromiumProjects,
        };
      }
    }

    public IGlobalSettingChangeListener<T> CreateChangeListener<T>(PropertyInfo propertyInfo) {
      return new GlobalSettingChangeListener<T>(_eventBus, this, propertyInfo);
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