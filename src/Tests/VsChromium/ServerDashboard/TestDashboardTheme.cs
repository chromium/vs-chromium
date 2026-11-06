// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Features.ServerDashboard;
using VsChromium.ServerProxy;
using VsChromium.Settings;

namespace VsChromium.Tests.VsChromium.ServerDashboard {
  [TestClass]
  public class TestDashboardTheme {
    private static DashboardThemeInput DarkTheme(bool highContrast = false) {
      return new DashboardThemeInput {
        Colors = new Dictionary<ThemeColor, ThemeRgba> {
          { ThemeColor.Background, new ThemeRgba(0x1f, 0x1f, 0x1f) },
          { ThemeColor.Foreground, new ThemeRgba(0xf1, 0xf1, 0xf1) },
          { ThemeColor.Border, new ThemeRgba(0x3f, 0x3f, 0x46) },
          { ThemeColor.Link, new ThemeRgba(0x55, 0xaa, 0xff) },
          { ThemeColor.PrimaryButtonBackground, new ThemeRgba(0x0e, 0x63, 0x9c) },
          { ThemeColor.CheckBoxGlyph, new ThemeRgba(0xf1, 0xf1, 0xf1, 128) },
        },
        FontFamily = "Segoe UI",
        FontSize = 12,
        EditorFontFamily = "Cascadia Mono",
        HighContrast = highContrast,
      };
    }

    [TestMethod]
    public void MapsTheColorsOfTheThemeToTheVariablesOfThePage() {
      var v = DashboardTheme.BuildVariables(DarkTheme());
      Assert.AreEqual("#1f1f1f", v["--vscode-editor-background"]);
      Assert.AreEqual("#f1f1f1", v["--vscode-foreground"]);
      Assert.AreEqual("#3f3f46", v["--vscode-panel-border"]);
      Assert.AreEqual("#55aaff", v["--vscode-textLink-foreground"]);
      Assert.AreEqual("#0e639c", v["--vscode-button-background"]);
      Assert.AreEqual("rgba(241, 241, 241, 0.502)", v["--vscode-checkbox-foreground"]);
      Assert.AreEqual(v["--vscode-checkbox-foreground"], v["--vscode-settings-checkboxForeground"]);
    }

    [TestMethod]
    public void DefinesAllTheVariablesUsedByThePage() {
      var v = DashboardTheme.BuildVariables(DarkTheme(highContrast: true));
      // The variables of media/dashboard.css (VS Code extension).
      var css = Path.Combine(TestContextDirectory(), "src", "vscode", "media", "dashboard.css");
      var used = Regex.Matches(File.ReadAllText(css), "--vscode-[A-Za-z0-9-]+").Cast<Match>().Select(m => m.Value).Distinct();
      var missing = used.Where(name => !v.ContainsKey(name)).ToList();
      CollectionAssert.AreEqual(new List<string>(), missing, "Not defined: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void UsesTheChartPaletteMatchingTheBackground() {
      var dark = DashboardTheme.BuildVariables(DarkTheme());
      var light = DashboardTheme.BuildVariables(new DashboardThemeInput {
        Colors = new Dictionary<ThemeColor, ThemeRgba> { { ThemeColor.Background, new ThemeRgba(0xf5, 0xf5, 0xf5) } },
      });
      Assert.AreEqual("#3794ff", dark["--vscode-charts-blue"]);
      Assert.AreEqual("#1a85ff", light["--vscode-charts-blue"]);
      Assert.AreEqual("#f48771", dark["--vscode-errorForeground"]);
      Assert.AreEqual("#a1260d", light["--vscode-errorForeground"]);
    }

    [TestMethod]
    public void DefinesTheContrastBordersOnlyInHighContrast() {
      Assert.IsFalse(DashboardTheme.BuildVariables(DarkTheme()).ContainsKey("--vscode-contrastBorder"));
      var hc = DashboardTheme.BuildVariables(DarkTheme(highContrast: true));
      Assert.AreEqual("#3f3f46", hc["--vscode-contrastBorder"]);
      Assert.IsTrue(hc.ContainsKey("--vscode-contrastActiveBorder"));
      Assert.IsTrue(hc.ContainsKey("--vscode-button-border"));
    }

    [TestMethod]
    public void MapsTheFonts() {
      var v = DashboardTheme.BuildVariables(DarkTheme());
      Assert.AreEqual("\"Segoe UI\", system-ui, sans-serif", v["--vscode-font-family"]);
      Assert.AreEqual("12px", v["--vscode-font-size"]);
      Assert.AreEqual("\"Cascadia Mono\", Consolas, \"Courier New\", monospace", v["--vscode-editor-font-family"]);
      Assert.AreEqual("\"Evil\", x", DashboardTheme.CssFontFamily("Evil\"; } {", "x"), "values stay a single declaration");
      Assert.AreEqual("x", DashboardTheme.CssFontFamily(" ", "x"));
    }

    [TestMethod]
    public void NoValueCanEscapeTheDeclaration() {
      foreach (var value in DashboardTheme.BuildVariables(DarkTheme(highContrast: true)).Values) {
        Assert.IsFalse(value.IndexOfAny(new[] { ';', '{', '}' }) >= 0, value);
      }
    }

    [TestMethod]
    public void TheServerArgumentsContainTheCacheDirectory() {
      Assert.AreEqual("--jsonrpc", ServerProcessLauncher.GetArguments(new IndexServerSettings(false, true, "")));
      Assert.AreEqual("--jsonrpc", ServerProcessLauncher.GetArguments(new IndexServerSettings(false, true, "  ")));
      Assert.AreEqual("--jsonrpc --cache-dir \"D:\\Cache Dir\"", ServerProcessLauncher.GetArguments(new IndexServerSettings(false, true, " D:\\Cache Dir ")));
      Assert.AreEqual("--jsonrpc --cache-dir \"D:\\.\"", ServerProcessLauncher.GetArguments(new IndexServerSettings(false, true, "D:\\")));
    }

    /// <summary>The root of the repository (the directory containing "build.root").</summary>
    private static string TestContextDirectory() {
      var directory = new DirectoryInfo(Path.GetDirectoryName(typeof(TestDashboardTheme).Assembly.Location));
      while (directory != null && !File.Exists(Path.Combine(directory.FullName, "build.root"))) {
        directory = directory.Parent;
      }
      Assert.IsNotNull(directory, "build.root not found");
      return directory.FullName;
    }
  }

  [TestClass]
  public class TestProjectOptionsStore {
    private string _file;

    [TestInitialize]
    public void Initialize() {
      _file = Path.Combine(Path.GetTempPath(), "VsChromiumTests", Path.GetRandomFileName(), "ProjectOptions.json");
    }

    [TestCleanup]
    public void Cleanup() {
      var directory = Path.GetDirectoryName(_file);
      if (Directory.Exists(directory)) {
        Directory.Delete(directory, true);
      }
    }

    [TestMethod]
    public void StoresTheOverridesPerProjectCaseInsensitively() {
      var store = new ProjectOptionsStore(_file);
      Assert.IsTrue(store.Get("C:\\src\\chromium").IsEmpty);
      store.Update("C:\\src\\chromium", o => o.TrigramFiltersEnabled = true);
      store.Update("C:\\src\\chromium", o => o.CacheEnabled = false);

      var reloaded = new ProjectOptionsStore(_file);
      var value = reloaded.Get("c:\\SRC\\chromium");
      Assert.AreEqual(true, value.TrigramFiltersEnabled);
      Assert.AreEqual(false, value.CacheEnabled);
      Assert.IsTrue(reloaded.Get("D:\\other").IsEmpty);
    }

    [TestMethod]
    public void RemovesEmptyOverrides() {
      var store = new ProjectOptionsStore(_file);
      store.Update("C:\\a", o => o.CacheEnabled = false);
      store.Update("C:\\a", o => o.CacheEnabled = null);
      StringAssert.Contains(File.ReadAllText(_file), "\"projects\": {}");
    }

    [TestMethod]
    public void IgnoresACorruptedFile() {
      Directory.CreateDirectory(Path.GetDirectoryName(_file));
      File.WriteAllText(_file, "{ not json");
      var store = new ProjectOptionsStore(_file);
      Assert.IsTrue(store.Get("C:\\a").IsEmpty);
      store.Update("C:\\a", o => o.TrigramFiltersEnabled = true);
      Assert.AreEqual(true, new ProjectOptionsStore(_file).Get("C:\\a").TrigramFiltersEnabled);
    }

    [TestMethod]
    public void TheOverrideWinsOverTheDefaults() {
      var none = ProjectOptionsStore.GetEffectiveOptions(new ProjectOptionsOverride(), true, false);
      Assert.AreEqual(true, none.TrigramFiltersEnabled);
      Assert.AreEqual(false, none.CacheEnabled);
      var both = ProjectOptionsStore.GetEffectiveOptions(new ProjectOptionsOverride { TrigramFiltersEnabled = false, CacheEnabled = true }, true, false);
      Assert.AreEqual(false, both.TrigramFiltersEnabled);
      Assert.AreEqual(true, both.CacheEnabled);
    }

    [TestMethod]
    public void WatchesTheExternalLinkTargetsOfAProjectOnlyIfEnabled() {
      Assert.AreEqual(false, ProjectOptionsStore.GetEffectiveOptions(new ProjectOptionsOverride(), false, true).WatchExternalLinkTargets,
        "Off by default");
      Assert.AreEqual(true, ProjectOptionsStore.GetEffectiveOptions(new ProjectOptionsOverride(), false, true, true).WatchExternalLinkTargets);
      var store = new ProjectOptionsStore(_file);
      store.Update("C:\\a", o => o.WatchExternalLinkTargets = true);
      var value = new ProjectOptionsStore(_file).Get("C:\\a");
      Assert.IsFalse(value.IsEmpty);
      Assert.AreEqual(true, value.WatchExternalLinkTargets);
      Assert.AreEqual(true, ProjectOptionsStore.GetEffectiveOptions(value, false, true, false).WatchExternalLinkTargets);
    }
  }
}
