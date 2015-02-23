// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Package;
using VsChromium.Settings;
using VsChromium.ToolsOptions;

namespace VsChromium.Tests.VsChromium.ToolsOptions {
  [TestClass]
  public class TestGlobalSettings {
    [TestMethod]
    public void GlobalSettingsCopyToToolsOptionsWorks() {
      var provider = new GlobalSettingsProvider(
        new ToolsOptionsPageProviderMock(),
        new EventBus());

      // Force reading settings from empty tools|options pages.
      var x = provider.GlobalSettings.EnableVsChromiumProjects;

      // Force writing settings to tools|options page.
      provider.GlobalSettings.EnableVsChromiumProjects = !x;
    }

    [TestMethod]
    public void CheckClassHasNotBeenRenamed() {
      // The Tools|Options pages should not be renamed, as this would break
      // compatibilty of saved settings with previous versions.
      Assert.AreEqual("VsChromium.ToolsOptions.GeneralOptions", typeof(GeneralOptions).FullName);
      Assert.AreEqual("VsChromium.ToolsOptions.CodingStyleOptions", typeof(CodingStyleOptions).FullName);
      Assert.AreEqual("VsChromium.ToolsOptions.DebuggingOptions", typeof(DebuggingOptions).FullName);
    }

    public class ToolsOptionsPageProviderMock : IToolsOptionsPageProvider {
      private readonly ConcurrentDictionary<Type, object> _objects  = new ConcurrentDictionary<Type, object>(); 
      public T GetToolsOptionsPage<T>() where T : DialogPage, new() {
        return (T)_objects.GetOrAdd(typeof (T), key => new T());
      }
    }
  }
}
