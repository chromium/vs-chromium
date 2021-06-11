// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Threading;
using VsChromium.Package;
using VsChromium.Settings;
using VsChromium.Threads;
using VsChromium.ToolsOptions;

namespace VsChromium.Tests.VsChromium.ToolsOptions {
  [TestClass]
  public class TestGlobalSettings {
    [TestMethod]
    public void GlobalSettingsCopyToToolsOptionsWorks() {
      var provider = new GlobalSettingsProvider(
        new ToolsOptionsPageProviderMock(),
        new DispatchThreadEventBus(new MyProvider()));

      // Force reading settings from empty tools|options pages.
      var x = provider.GlobalSettings.EnableSourceExplorerHierarchy;

      // Force writing settings to tools|options page.
      provider.GlobalSettings.EnableSourceExplorerHierarchy = !x;
    }

    [TestMethod]
    public void CheckClassHasNotBeenRenamed() {
      // The Tools|Options pages should not be renamed, as this would break
      // compatibilty of saved settings with previous versions.
      Assert.AreEqual("VsChromium.ToolsOptions.GeneralOptions", typeof(GeneralOptions).FullName);
      Assert.AreEqual("VsChromium.ToolsOptions.CodingStyleOptions", typeof(CodingStyleOptions).FullName);
      Assert.AreEqual("VsChromium.ToolsOptions.DebuggingOptions", typeof(DebuggingOptions).FullName);
    }

    private class CodingStyleOptionsMock : CodingStyleOptions {
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "VSSDK005:Avoid instantiating JoinableTaskContext", Justification = "Necessary for tests.")]
      public CodingStyleOptionsMock() : base(new JoinableTaskContext()) {
      }

      public override void SaveSettingsToStorage() {
      }
    }

    private class GeneralOptionsMock : GeneralOptions {
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "VSSDK005:Avoid instantiating JoinableTaskContext", Justification = "Necessary for tests.")]
      public GeneralOptionsMock() : base(new JoinableTaskContext()) {
      }

      public override void SaveSettingsToStorage() {
      }
    }

    private class ToolsOptionsPageProviderMock : IToolsOptionsPageProvider {
      private readonly ConcurrentDictionary<Type, object> _objects  = new ConcurrentDictionary<Type, object>(); 
      public T GetToolsOptionsPage<T>() where T : DialogPage, new() {
        return (T)_objects.GetOrAdd(typeof(T), key => this.CreateInstance<T>());
      }

      object CreateInstance<T>() where T : new() {
        switch (typeof(T))
        {
          case var t when t == typeof(CodingStyleOptions):
            return new CodingStyleOptionsMock();
          case var t when t == typeof(GeneralOptions):
            return new GeneralOptionsMock();
          default:
            return new T();
        }
      }
    }

    private class MyProvider : ISynchronizationContextProvider {
      public ISynchronizationContext DispatchThreadContext {
        get {
          return new MyContext();
        }
      }

      public class MyContext : ISynchronizationContext {
        public void Post(Action action) {
        }
      }
    }
  }
}
