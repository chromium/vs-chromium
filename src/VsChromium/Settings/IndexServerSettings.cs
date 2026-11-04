// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using VsChromium.Package;
using VsChromium.ToolsOptions;

namespace VsChromium.Settings {
  /// <summary>The values of the "Index Server" options page at a point in time (immutable).</summary>
  public sealed class IndexServerSettings {
    public IndexServerSettings(bool trigramFiltersEnabled, bool cacheEnabled, string cacheDirectory) {
      TrigramFiltersEnabled = trigramFiltersEnabled;
      CacheEnabled = cacheEnabled;
      CacheDirectory = cacheDirectory ?? "";
    }

    public bool TrigramFiltersEnabled { get; }
    public bool CacheEnabled { get; }
    public string CacheDirectory { get; }
  }

  /// <summary>
  /// The settings of the indexing server (<see cref="IndexServerOptions"/>),
  /// readable from any thread (e.g. when the server starts on the thread pool).
  /// </summary>
  public interface IIndexServerSettingsProvider {
    IndexServerSettings Settings { get; }

    /// <summary>Raised (on the UI thread) when the settings change.</summary>
    event EventHandler SettingsChanged;

    /// <summary>Sets the default of the trigram filters (UI thread).</summary>
    void SetTrigramFiltersEnabled(bool enabled);

    /// <summary>Shows the options page (UI thread).</summary>
    void ShowOptionsPage();
  }

  [Export(typeof(IIndexServerSettingsProvider))]
  [Export(typeof(IPackagePreInitializer))]
  public class IndexServerSettingsProvider : IIndexServerSettingsProvider, IPackagePreInitializer {
    private readonly IVisualStudioPackageProvider _packageProvider;
    private readonly IDispatchThreadEventBus _eventBus;
    private volatile IndexServerSettings _settings = new IndexServerSettings(false, true, "");

    [ImportingConstructor]
    public IndexServerSettingsProvider(IVisualStudioPackageProvider packageProvider, IDispatchThreadEventBus eventBus) {
      _packageProvider = packageProvider;
      _eventBus = eventBus;
    }

    public IndexServerSettings Settings => _settings;

    public event EventHandler SettingsChanged;

    /// <summary>Before the other initializers: the settings are read before the server starts.</summary>
    public int Priority => 100;

    public void Run(IVisualStudioPackage package) {
      Refresh();
      _eventBus.RegisterHandler(EventNames.ToolsOptions.PageApply, (sender, args) => {
        if (sender is IndexServerOptions) {
          Refresh();
          SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
      });
    }

    public void SetTrigramFiltersEnabled(bool enabled) {
      var page = _packageProvider.Package.GetToolsOptionsPage<IndexServerOptions>();
      page.TrigramFiltersEnabled = enabled;
      page.SaveSettingsToStorage();
      Refresh();
      SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ShowOptionsPage() {
      _packageProvider.Package.ShowOptionPage(typeof(IndexServerOptions));
    }

    private void Refresh() {
      var page = _packageProvider.Package.GetToolsOptionsPage<IndexServerOptions>();
      _settings = new IndexServerSettings(page.TrigramFiltersEnabled, page.CacheEnabled, page.CacheDirectory);
    }
  }
}
