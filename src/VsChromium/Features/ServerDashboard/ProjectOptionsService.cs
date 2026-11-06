// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using VsChromium.Core.Logging;
using VsChromium.Package;
using VsChromium.Protocol.Generated;
using VsChromium.ServerProxy;
using VsChromium.Settings;

namespace VsChromium.Features.ServerDashboard {
  /// <summary>The per project options of Visual Studio (see <see cref="ProjectOptionsStore"/>).</summary>
  public interface IProjectOptionsService {
    ProjectOptionsStore Store { get; }

    /// <summary>
    /// Sends the options of the projects known to the server
    /// ("project/setOptions"): the override of each project, otherwise the
    /// defaults of the "Index Server" options.
    /// </summary>
    Task ApplyAsync();
  }

  /// <summary>
  /// Sends the per project options to the server when it starts, when new
  /// projects are loaded ("index/loaded") and when the settings change
  /// (the server keeps the options for its lifetime only).
  /// </summary>
  [Export(typeof(IProjectOptionsService))]
  [Export(typeof(IPackagePostInitializer))]
  public class ProjectOptionsService : IProjectOptionsService, IPackagePostInitializer {
    private readonly IServerClient _serverClient;
    private readonly IIndexServerSettingsProvider _settingsProvider;

    [ImportingConstructor]
    public ProjectOptionsService(IServerClient serverClient, IIndexServerSettingsProvider settingsProvider) {
      _serverClient = serverClient;
      _settingsProvider = settingsProvider;
      Store = new ProjectOptionsStore(ProjectOptionsStore.DefaultFilePath);
    }

    public ProjectOptionsStore Store { get; }

    public int Priority => 0;

    public void Run(IVisualStudioPackage package) {
      _serverClient.ProcessStarted += (sender, args) => ApplyInBackground();
      // Note: Projects discovered since the last load (e.g. from an opened file).
      _serverClient.IndexLoaded += parameters => ApplyInBackground();
      _settingsProvider.SettingsChanged += (sender, args) => ApplyInBackground();
    }

    private void ApplyInBackground() {
      ApplyAsync().FileAndForget("VsChromium/ProjectOptions/Apply");
    }

    public async Task ApplyAsync() {
      if (!_serverClient.IsServerRunning) {
        return;
      }
      try {
        var client = await _serverClient.GetProtocolClientAsync().ConfigureAwait(false);
        var tree = await client.TreeGetAsync(new TreeParams()).ConfigureAwait(false);
        var settings = _settingsProvider.Settings;
        foreach (var project in tree.Projects) {
          var options = ProjectOptionsStore.GetEffectiveOptions(Store.Get(project.RootPath),
            settings.TrigramFiltersEnabled, settings.CacheEnabled, settings.WatchExternalLinkTargets);
          await client.ProjectSetOptionsAsync(new SetProjectOptionsParams {
            ProjectPath = project.RootPath,
            TrigramFiltersEnabled = options.TrigramFiltersEnabled,
            CacheEnabled = options.CacheEnabled,
            WatchExternalLinkTargets = options.WatchExternalLinkTargets,
          }).ConfigureAwait(false);
        }
      }
      catch (Exception e) {
        Logger.LogWarn(e, "Error sending the project options to the server.");
      }
    }
  }
}
