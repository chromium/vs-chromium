// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VsChromium.Features.ServerDashboard {
  /// <summary>
  /// The server, as seen by the dashboard: raw JSON-RPC requests (method name
  /// and JSON parameters, as sent by the page) and notifications.
  /// </summary>
  public interface IDashboardServer {
    /// <summary>True when the server is started and the connection is alive.</summary>
    bool IsRunning { get; }

    /// <summary>The version of the server, once started.</summary>
    string ServerVersion { get; }

    /// <summary>Why the server is not running (e.g. a start error), if known.</summary>
    string StoppedReason { get; }

    /// <summary>
    /// Sends a request to the server. <paramref name="parameters"/> is null
    /// for the requests without parameters.
    /// </summary>
    Task<JToken> InvokeAsync(string method, JToken parameters, CancellationToken cancellationToken);

    /// <summary>
    /// The "index/..." notifications of the server (method name, parameters).
    /// Raised on any thread.
    /// </summary>
    event Action<string, JToken> Notification;

    /// <summary>Raised when the server is started or stopped (any thread).</summary>
    event EventHandler StateChanged;
  }

  /// <summary>
  /// The host side of the dashboard: the browser control (messages to the
  /// page), the settings and the commands of the host.
  /// </summary>
  public interface IDashboardHost {
    /// <summary>Sends a message (JSON) to the page. Can be called on any thread.</summary>
    void PostMessage(string json);

    /// <summary>The state of the host for the page (settings, log directory, texts).</summary>
    DashboardHostState GetHostState();

    /// <summary>
    /// Executes a command of the page (see <see cref="DashboardProtocol.Commands"/>).
    /// <paramref name="message"/> contains the arguments of the command
    /// (e.g. "projectPath", "enabled").
    /// </summary>
    Task ExecuteCommandAsync(string command, JObject message);

    /// <summary>The message of an error of a request, for display.</summary>
    string GetErrorMessage(Exception error);
  }

  /// <summary>The settings and texts of the host sent to the page ("hostState").</summary>
  public class DashboardHostState {
    public bool TrigramFiltersSetting { get; set; }
    public string LogDirectory { get; set; }
    public bool CacheEnabled { get; set; }
    public string CacheDirectory { get; set; }
    /// <summary>Host specific texts (<c>HostLabels</c> of dashboardMessages.ts).</summary>
    public IDictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();
  }
}
