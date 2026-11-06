// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json.Linq;
using VsChromium.Core.Logging;
using VsChromium.Package;
using VsChromium.Protocol.Generated;
using VsChromium.ServerProxy;
using VsChromium.Settings;

namespace VsChromium.Features.ServerDashboard {
  /// <summary>
  /// <see cref="IDashboardHost"/> in Visual Studio: the settings (the "Index
  /// Server" options page and the per project options of
  /// <see cref="IProjectOptionsService"/>) and the commands of the page
  /// (confirmations in message boxes, folders opened in the File Explorer).
  /// </summary>
  public class VsDashboardHost : IDashboardHost {
    private readonly IServerClient _serverClient;
    private readonly IIndexServerSettingsProvider _settingsProvider;
    private readonly IProjectOptionsService _projectOptions;
    private readonly IServiceProvider _serviceProvider;
    private readonly Action<string> _postMessage;

    public VsDashboardHost(IServerClient serverClient, IIndexServerSettingsProvider settingsProvider,
                           IProjectOptionsService projectOptions, IServiceProvider serviceProvider, Action<string> postMessage) {
      _serverClient = serverClient;
      _settingsProvider = settingsProvider;
      _projectOptions = projectOptions;
      _serviceProvider = serviceProvider;
      _postMessage = postMessage;
    }

    /// <summary>The directory of the log files (extension and server).</summary>
    public static string LogDirectory => Path.GetDirectoryName(Logger.LogInfoPath);

    public void PostMessage(string json) {
      _postMessage(json);
    }

    public DashboardHostState GetHostState() {
      var settings = _settingsProvider.Settings;
      return new DashboardHostState {
        TrigramFiltersSetting = settings.TrigramFiltersEnabled,
        CacheEnabled = settings.CacheEnabled,
        CacheDirectory = settings.CacheDirectory,
        LogDirectory = LogDirectory,
        Labels = new Dictionary<string, string> {
          ["trigramFiltersDefault"] =
            "Use trigram filters by default (Tools > Options > VS Chromium > Index Server): much faster selective searches, " +
            "at the cost of memory (about 5% of the size of the indexed text). Each project can override it below " +
            "(saved for this user, whatever the solution).",
        },
      };
    }

    public string GetErrorMessage(Exception error) {
      var serverError = ServerError.FromException(error);
      return serverError.GetBaseError().Message;
    }

    public async Task ExecuteCommandAsync(string command, JObject message) {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      var projectPath = (string)message["projectPath"];
      var enabled = (bool?)message["enabled"] ?? false;
      switch (command) {
        case "restartServer":
          await _serverClient.RestartAsync();
          break;
        case "showOutput":
          // Visual Studio: the log of the extension (there is no output pane).
          if (File.Exists(Logger.LogInfoPath)) {
            VsShellUtilities.OpenDocument(_serviceProvider, Logger.LogInfoPath);
          }
          break;
        case "openLogDirectory":
          OpenFolder(LogDirectory);
          break;
        case "openCacheSettings":
          _settingsProvider.ShowOptionsPage();
          break;
        case "openProjectCacheFolder":
          await OpenProjectCacheFolderAsync(projectPath);
          break;
        case "setTrigramFiltersSetting":
          _settingsProvider.SetTrigramFiltersEnabled(enabled);
          break;
        case "setProjectTrigramFilters":
          _projectOptions.Store.Update(projectPath, o => o.TrigramFiltersEnabled = enabled);
          await _projectOptions.ApplyAsync();
          break;
        case "setProjectCacheEnabled":
          if (!enabled && !Confirm(string.Format("Disable the index cache of \"{0}\"?", projectPath),
                "Its cache is deleted: the next starts of the server read all the files of the project.")) {
            break;
          }
          _projectOptions.Store.Update(projectPath, o => o.CacheEnabled = enabled);
          await _projectOptions.ApplyAsync();
          break;
        case "setProjectWatchExternalLinkTargets":
          _projectOptions.Store.Update(projectPath, o => o.WatchExternalLinkTargets = enabled);
          await _projectOptions.ApplyAsync();
          break;
        case "clearCache":
          await ClearCacheAsync(projectPath);
          break;
        case "rebuildProject":
          await RebuildProjectAsync(projectPath);
          break;
        case "copyToClipboard":
          CopyToClipboard((string)message["text"]);
          break;
        case "addIgnorePattern":
          await AddIgnorePatternAsync(projectPath, (string)message["section"], (string)message["pattern"]);
          break;
      }
    }

    private static void CopyToClipboard(string text) {
      if (string.IsNullOrEmpty(text)) {
        return;
      }
      try {
        System.Windows.Clipboard.SetText(text);
      }
      catch (ExternalException e) {
        Logger.LogWarn(e, "Failed to copy dashboard text to clipboard.");
      }
    }

    private async Task AddIgnorePatternAsync(string projectPath, string sectionValue, string pattern) {
      if (string.IsNullOrEmpty(projectPath) || string.IsNullOrEmpty(pattern)) {
        return;
      }
      IgnoreSection section;
      string sectionName;
      string effect;
      if (sectionValue == "sourceExplorerIgnore") {
        section = IgnoreSection.SourceExplorerIgnore;
        sectionName = "SourceExplorer.ignore";
        effect = "Matching files and directories will be excluded from the project tree, file name search, and code search.";
      } else if (sectionValue == "searchableFilesIgnore") {
        section = IgnoreSection.SearchableFilesIgnore;
        sectionName = "SearchableFiles.ignore";
        effect = "Matching files will remain in the project tree and file name search, but their contents will be excluded from code search.";
      } else {
        Logger.LogWarn("Unknown ignore section \"{0}\".", sectionValue);
        return;
      }

      if (!Confirm(
            string.Format("Add \"{0}\" to [{1}] in \"{2}\"?", pattern, sectionName, projectPath),
            effect + "\r\n\r\nUpdating vs-chromium-project.txt triggers a rescan of the project.")) {
        return;
      }

      try {
        var client = await _serverClient.GetProtocolClientAsync();
        var result = await client.ProjectAddIgnorePatternAsync(new AddIgnorePatternParams {
          ProjectPath = projectPath,
          Section = section,
          Pattern = pattern,
        });
        if (!result.Added) {
          await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
          VsShellUtilities.ShowMessageBox(
            _serviceProvider,
            string.Format("\"{0}\" is already present in [{1}] ({2}).", pattern, result.SectionName, result.ProjectFile),
            "VsChromium",
            OLEMSGICON.OLEMSGICON_INFO,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
      }
      catch (Exception e) {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        VsShellUtilities.ShowMessageBox(
          _serviceProvider,
          GetErrorMessage(e),
          "VsChromium",
          OLEMSGICON.OLEMSGICON_CRITICAL,
          OLEMSGBUTTON.OLEMSGBUTTON_OK,
          OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
      }
    }

    private async Task ClearCacheAsync(string projectPath) {
      var question = projectPath != null
        ? string.Format("Clear the index cache of \"{0}\"?", projectPath)
        : "Clear the index cache of all the projects?";
      if (!Confirm(question, "The cache files are deleted. The current index stays in use; the cache is written again " +
                             "once the index settles. Until then, a restarted server indexes the files from disk.")) {
        return;
      }
      var client = await _serverClient.GetProtocolClientAsync();
      if (projectPath != null) {
        await client.ProjectClearCacheAsync(new ProjectPathParams { ProjectPath = projectPath });
      } else {
        await client.IndexClearCacheAsync();
      }
    }

    private async Task RebuildProjectAsync(string projectPath) {
      if (!Confirm(string.Format("Rebuild the index of \"{0}\"?", projectPath),
            "The cache of the project is deleted, then its files are scanned and read again. Searches use the current " +
            "index until the new one is ready. The other projects are not affected.")) {
        return;
      }
      var client = await _serverClient.GetProtocolClientAsync();
      await client.ProjectRebuildAsync(new ProjectPathParams { ProjectPath = projectPath });
    }

    /// <summary>The cache directory of a project, as reported by the server (not by the page).</summary>
    private async Task OpenProjectCacheFolderAsync(string projectPath) {
      var client = await _serverClient.GetProtocolClientAsync();
      var details = await client.ProjectDetailsAsync(new ProjectDetailsParams {
        ProjectPath = projectPath,
        MaxFilesByExtensionDetailsCount = 0,
        MaxLargeFilesDetailsCount = 0,
      });
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      var directory = details.Cache?.Directory;
      if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) {
        VsShellUtilities.ShowMessageBox(_serviceProvider,
          string.Format("The project \"{0}\" has no cache directory yet.", projectPath), "VsChromium",
          OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        return;
      }
      OpenFolder(directory);
    }

    private static void OpenFolder(string directory) {
      if (!Directory.Exists(directory)) {
        return;
      }
      Process.Start(new ProcessStartInfo("explorer.exe", "\"" + directory + "\"") { UseShellExecute = false });
    }

    private bool Confirm(string question, string details) {
      ThreadHelper.ThrowIfNotOnUIThread();
      var result = VsShellUtilities.ShowMessageBox(_serviceProvider, question + "\r\n\r\n" + details, "VsChromium",
        OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);
      return result == 1; // IDOK
    }
  }
}
