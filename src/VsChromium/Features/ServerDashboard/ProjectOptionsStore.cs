// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VsChromium.Core.Logging;

namespace VsChromium.Features.ServerDashboard {
  /// <summary>The options of a project overriding the defaults (null: the default applies).</summary>
  public class ProjectOptionsOverride {
    public bool? TrigramFiltersEnabled { get; set; }
    public bool? CacheEnabled { get; set; }
    /// <summary>Watch the targets of the symbolic links outside of the project roots.</summary>
    public bool? WatchExternalLinkTargets { get; set; }

    public bool IsEmpty => TrigramFiltersEnabled == null && CacheEnabled == null && WatchExternalLinkTargets == null;
  }

  /// <summary>The options sent to the server for a project ("project/setOptions").</summary>
  public struct EffectiveProjectOptions {
    public EffectiveProjectOptions(bool trigramFiltersEnabled, bool cacheEnabled, bool watchExternalLinkTargets = false) {
      TrigramFiltersEnabled = trigramFiltersEnabled;
      CacheEnabled = cacheEnabled;
      WatchExternalLinkTargets = watchExternalLinkTargets;
    }

    public bool TrigramFiltersEnabled { get; }
    public bool CacheEnabled { get; }
    public bool WatchExternalLinkTargets { get; }
  }

  /// <summary>
  /// The per project options of Visual Studio (Visual Studio has no per folder
  /// settings like VS Code): the defaults are the "Index Server" options page
  /// (Tools > Options > VS Chromium), the overrides set from the dashboard are
  /// stored per user in a JSON file (by project root path, case-insensitive),
  /// so they apply whatever solution is open (VsChromium projects are
  /// directories, not solutions).
  /// <para>Thread-safe.</para>
  /// </summary>
  public class ProjectOptionsStore {
    private readonly string _filePath;
    private readonly object _lock = new object();
    private Dictionary<string, ProjectOptionsOverride> _overrides;

    public ProjectOptionsStore(string filePath) {
      _filePath = filePath;
    }

    /// <summary>The default store: %LOCALAPPDATA%\VsChromium\ProjectOptions.json.</summary>
    public static string DefaultFilePath =>
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VsChromium", "ProjectOptions.json");

    /// <summary>The override of a project (empty if none).</summary>
    public ProjectOptionsOverride Get(string projectPath) {
      lock (_lock) {
        ProjectOptionsOverride value;
        if (Load().TryGetValue(projectPath, out value)) {
          return new ProjectOptionsOverride {
            TrigramFiltersEnabled = value.TrigramFiltersEnabled,
            CacheEnabled = value.CacheEnabled,
            WatchExternalLinkTargets = value.WatchExternalLinkTargets,
          };
        }
        return new ProjectOptionsOverride();
      }
    }

    /// <summary>
    /// Sets the override of a project (removed if empty) and saves the store.
    /// <paramref name="update"/> modifies the current override.
    /// </summary>
    public void Update(string projectPath, Action<ProjectOptionsOverride> update) {
      lock (_lock) {
        var overrides = Load();
        ProjectOptionsOverride value;
        if (!overrides.TryGetValue(projectPath, out value)) {
          value = new ProjectOptionsOverride();
        }
        update(value);
        if (value.IsEmpty) {
          overrides.Remove(projectPath);
        } else {
          overrides[projectPath] = value;
        }
        Save(overrides);
      }
    }

    /// <summary>
    /// The options of a project: its override, otherwise the defaults. An
    /// override equal to the default is kept (the default may change later).
    /// </summary>
    public static EffectiveProjectOptions GetEffectiveOptions(ProjectOptionsOverride projectOverride,
                                                              bool defaultTrigramFilters, bool defaultCacheEnabled,
                                                              bool defaultWatchExternalLinkTargets = false) {
      return new EffectiveProjectOptions(
        projectOverride?.TrigramFiltersEnabled ?? defaultTrigramFilters,
        projectOverride?.CacheEnabled ?? defaultCacheEnabled,
        projectOverride?.WatchExternalLinkTargets ?? defaultWatchExternalLinkTargets);
    }

    private Dictionary<string, ProjectOptionsOverride> Load() {
      if (_overrides != null) {
        return _overrides;
      }
      var result = new Dictionary<string, ProjectOptionsOverride>(StringComparer.OrdinalIgnoreCase);
      try {
        if (File.Exists(_filePath)) {
          var root = JObject.Parse(File.ReadAllText(_filePath));
          var projects = root["projects"] as JObject;
          if (projects != null) {
            foreach (var property in projects.Properties()) {
              var value = property.Value.ToObject<ProjectOptionsOverride>();
              if (value != null && !value.IsEmpty) {
                result[property.Name] = value;
              }
            }
          }
        }
      }
      catch (Exception e) {
        // Note: A corrupted file is ignored (overwritten by the next change).
        Logger.LogWarn(e, "Error reading the project options \"{0}\".", _filePath);
      }
      _overrides = result;
      return result;
    }

    private void Save(Dictionary<string, ProjectOptionsOverride> overrides) {
      var projects = new JObject();
      foreach (var entry in overrides) {
        projects[entry.Key] = JObject.FromObject(entry.Value, JsonSerializer.Create(new JsonSerializerSettings {
          NullValueHandling = NullValueHandling.Ignore,
        }));
      }
      var root = new JObject {
        ["comment"] = "VsChromium: per project options set from the server dashboard of Visual Studio.",
        ["projects"] = projects,
      };
      var directory = Path.GetDirectoryName(_filePath);
      if (!string.IsNullOrEmpty(directory)) {
        Directory.CreateDirectory(directory);
      }
      var temp = _filePath + ".tmp";
      File.WriteAllText(temp, root.ToString(Formatting.Indented));
      if (File.Exists(_filePath)) {
        File.Replace(temp, _filePath, null);
      } else {
        File.Move(temp, _filePath);
      }
    }
  }
}
