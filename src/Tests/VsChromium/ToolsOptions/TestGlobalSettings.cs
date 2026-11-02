// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Package;
using VsChromium.Settings;
using VsChromium.Threads;
using VsChromium.ToolsOptions;

namespace VsChromium.Tests.VsChromium.ToolsOptions {
  [TestClass]
  public class TestGlobalSettings {
    private const BindingFlags DeclaredPublicProperties = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;

    [TestMethod]
    public void GlobalSettingsCopyToToolsOptionsWorks() {
      var provider = new GlobalSettingsProvider(
        new ToolsOptionsPageProviderMock(),
        new DispatchThreadEventBus(new MyProvider()));

      // Force reading settings from empty tools|options pages.
      var x = provider.GlobalSettings.EnableSourceExplorerHierarchy;

      // Force writing settings to tools|options page.
      provider.GlobalSettings.EnableSourceExplorerHierarchy = !x;
      Assert.AreEqual(!x, provider.GlobalSettings.EnableSourceExplorerHierarchy);
    }

    [TestMethod]
    public void ToolsOptionsPagesPropertiesMapToGlobalSettings() {
      // GlobalSettingsProvider copies the declared public properties of the Tools|Options pages
      // to/from GlobalSettings (with a "CodingStyle" prefix for CodingStyleOptions), and throws
      // at runtime if a page property has no matching GlobalSettings property.
      AssertPropertiesMapToGlobalSettings(typeof(GeneralOptions), "");
      AssertPropertiesMapToGlobalSettings(typeof(CodingStyleOptions), "CodingStyle");
    }

    [TestMethod]
    public void CheckClassHasNotBeenRenamed() {
      // The Tools|Options pages should not be renamed, as this would break
      // compatibilty of saved settings with previous versions.
      Assert.AreEqual("VsChromium.ToolsOptions.GeneralOptions", typeof(GeneralOptions).FullName);
      Assert.AreEqual("VsChromium.ToolsOptions.CodingStyleOptions", typeof(CodingStyleOptions).FullName);
      Assert.AreEqual("VsChromium.ToolsOptions.DebuggingOptions", typeof(DebuggingOptions).FullName);
    }

    private static void AssertPropertiesMapToGlobalSettings(Type pageType, string prefix) {
      var pageProperties = pageType.GetProperties(DeclaredPublicProperties);
      Assert.IsTrue(pageProperties.Length > 0, "{0} should declare public properties", pageType.Name);
      foreach (var pageProperty in pageProperties) {
        var name = prefix + pageProperty.Name;
        var settingsProperty = typeof(GlobalSettings).GetProperty(name, DeclaredPublicProperties);
        Assert.IsNotNull(settingsProperty, "GlobalSettings should have a property named \"{0}\"", name);
        Assert.AreEqual(pageProperty.PropertyType, settingsProperty.PropertyType, "Type of property \"{0}\"", name);
      }
    }

    /// <summary>
    /// Creates an instance of a Tools|Options page type without running any constructor. The
    /// <see cref="DialogPage"/> constructors depend on Visual Studio internal assemblies that are
    /// only available when running inside Visual Studio (e.g. Microsoft.Internal.VisualStudio.Interop).
    /// Properties are initialized from their <see cref="DefaultValueAttribute"/>.
    /// </summary>
    private static T CreatePageWithoutConstructor<T>() where T : DialogPage {
      var page = (T)FormatterServices.GetUninitializedObject(typeof(T));
      foreach (var type in typeof(T).GetBaseTypes().TakeWhile(t => t != typeof(DialogPage))) {
        foreach (var property in type.GetProperties(DeclaredPublicProperties).Where(p => p.CanWrite)) {
          var defaultValue = property.GetCustomAttributes(typeof(DefaultValueAttribute), true)
            .OfType<DefaultValueAttribute>()
            .FirstOrDefault();
          if (defaultValue != null) {
            property.SetValue(page, defaultValue.Value);
          }
        }
      }
      return page;
    }

    private class CodingStyleOptionsMock : CodingStyleOptions {
      public override void SaveSettingsToStorage() {
      }
    }

    private class GeneralOptionsMock : GeneralOptions {
      public override void SaveSettingsToStorage() {
      }
    }

    private class ToolsOptionsPageProviderMock : IToolsOptionsPageProvider {
      private readonly ConcurrentDictionary<Type, object> _objects  = new ConcurrentDictionary<Type, object>(); 
      public T GetToolsOptionsPage<T>() where T : DialogPage, new() {
        return (T)_objects.GetOrAdd(typeof(T), key => this.CreateInstance<T>());
      }

      object CreateInstance<T>() where T : DialogPage, new() {
        switch (typeof(T))
        {
          case var t when t == typeof(CodingStyleOptions):
            return CreatePageWithoutConstructor<CodingStyleOptionsMock>();
          case var t when t == typeof(GeneralOptions):
            return CreatePageWithoutConstructor<GeneralOptionsMock>();
          default:
            return CreatePageWithoutConstructor<T>();
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

  internal static class TypeExtensions {
    /// <summary>
    /// Returns <paramref name="type"/> followed by all its base types.
    /// </summary>
    public static System.Collections.Generic.IEnumerable<Type> GetBaseTypes(this Type type) {
      for (var t = type; t != null; t = t.BaseType) {
        yield return t;
      }
    }
  }
}
