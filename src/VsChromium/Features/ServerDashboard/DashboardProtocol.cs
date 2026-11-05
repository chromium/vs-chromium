// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;

namespace VsChromium.Features.ServerDashboard {
  /// <summary>
  /// The messages exchanged with the server dashboard page (the same page as
  /// the VS Code extension: src/vscode/src/ui/dashboard/dashboardMessages.ts).
  /// The page sends protocol requests (forwarded as is to the server when
  /// allowed), and commands only the host can execute.
  /// </summary>
  public static class DashboardProtocol {
    /// <summary>
    /// The protocol requests the page may send (the same allow-list as
    /// <c>DashboardRequestMethods</c> of dashboardMessages.ts).
    /// </summary>
    public static readonly IReadOnlyCollection<string> RequestMethods = new HashSet<string>(StringComparer.Ordinal) {
      "index/statistics",
      "index/status",
      "index/clearCache",
      "index/details",
      "index/pause",
      "index/resume",
      "project/details",
      "project/setOptions",
      "project/rebuild",
      "project/clearCache",
      "project/directorySizes",
      "project/measureDirectory",
      "project/cancelMeasure",
      "server/statistics",
      "server/history",
      "tree/refresh",
    };

    /// <summary>
    /// The commands of the page executed by the host (<c>HostCommand</c> of
    /// dashboardMessages.ts).
    /// </summary>
    public static readonly IReadOnlyCollection<string> Commands = new HashSet<string>(StringComparer.Ordinal) {
      "restartServer",
      "showOutput",
      "openLogDirectory",
      "setTrigramFiltersSetting",
      "setProjectTrigramFilters",
      "clearCache",
      "openCacheSettings",
      "rebuildProject",
      "setProjectCacheEnabled",
      "openProjectCacheFolder",
      "copyToClipboard",
      "addIgnorePattern",
    };

    public static bool IsAllowedRequest(string method) {
      return method != null && ((HashSet<string>)RequestMethods).Contains(method);
    }

    public static bool IsCommand(string command) {
      return command != null && ((HashSet<string>)Commands).Contains(command);
    }
  }
}
