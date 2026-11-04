// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VsChromium.Core.Logging;

namespace VsChromium.Features.ServerDashboard {
  /// <summary>
  /// The host side of the server dashboard page, free of Visual Studio and
  /// WebView2 dependencies (unit tested): the equivalent of dashboardBridge.ts
  /// of the VS Code extension. Forwards the allowed protocol requests of the
  /// page to the server (method name and raw JSON parameters), the "index/..."
  /// notifications of the server to the page, sends the state of the host
  /// ("hostState"), the visibility and the theme, and executes the commands of
  /// the page through the host.
  /// </summary>
  public class DashboardBridge : IDisposable {
    public const string HostName = "Visual Studio";

    private readonly IDashboardServer _server;
    private readonly IDashboardHost _host;
    private volatile bool _disposed;

    public DashboardBridge(IDashboardServer server, IDashboardHost host) {
      _server = server;
      _host = host;
      _server.Notification += ServerOnNotification;
      _server.StateChanged += ServerOnStateChanged;
    }

    /// <summary>
    /// Raised when the page is loaded and listening (message "ready"): the
    /// host then sends the theme and the visibility.
    /// </summary>
    public event EventHandler PageReady;

    public void Dispose() {
      _disposed = true;
      _server.Notification -= ServerOnNotification;
      _server.StateChanged -= ServerOnStateChanged;
    }

    /// <summary>
    /// Handles a message of the page (the JSON of the posted object). Never
    /// throws: errors of requests are returned to the page, other errors are
    /// logged.
    /// </summary>
    public async Task HandleMessageAsync(string json) {
      JObject message;
      try {
        message = JObject.Parse(json);
      }
      catch (JsonException e) {
        Logger.LogWarn(e, "Invalid message from the dashboard page.");
        return;
      }

      var type = (string)message["type"];
      switch (type) {
        case "ready":
          PostHostState();
          PageReady?.Invoke(this, EventArgs.Empty);
          break;
        case "request":
          await ForwardRequestAsync(message);
          break;
        case "command":
          await ExecuteCommandAsync(message);
          break;
        default:
          Logger.LogWarn("Unknown message type from the dashboard page: \"{0}\".", type);
          break;
      }
    }

    private async Task ForwardRequestAsync(JObject message) {
      var idToken = message["id"];
      if (idToken == null || idToken.Type != JTokenType.Integer) {
        Logger.LogWarn("Dashboard request without id.");
        return;
      }
      var id = (long)idToken;
      var method = (string)message["method"];
      var response = new JObject { ["type"] = "response", ["id"] = id };
      if (!DashboardProtocol.IsAllowedRequest(method)) {
        response["error"] = string.Format("Request not allowed: {0}", method);
      } else if (!_server.IsRunning) {
        response["error"] = "The indexing server is not running.";
      } else {
        var parameters = message["params"];
        if (parameters != null && parameters.Type == JTokenType.Null) {
          parameters = null;
        }
        try {
          var result = await _server.InvokeAsync(method, parameters, CancellationToken.None);
          response["result"] = result ?? JValue.CreateNull();
        }
        catch (Exception e) {
          response["error"] = _host.GetErrorMessage(e);
        }
      }
      Post(response);
    }

    private async Task ExecuteCommandAsync(JObject message) {
      var command = (string)message["command"];
      if (!DashboardProtocol.IsCommand(command)) {
        Logger.LogWarn("Unknown command from the dashboard page: \"{0}\".", command);
        return;
      }
      try {
        await _host.ExecuteCommandAsync(command, message);
      }
      catch (Exception e) {
        Logger.LogError(e, "Error executing the dashboard command \"{0}\".", command);
      }
      // Note: The commands may change the settings of the host.
      PostHostState();
    }

    /// <summary>Sends the state of the host and of the server to the page.</summary>
    public void PostHostState() {
      var hostState = _host.GetHostState();
      var state = new JObject {
        ["hostName"] = HostName,
        ["serverRunning"] = _server.IsRunning,
        ["trigramFiltersSetting"] = hostState.TrigramFiltersSetting,
        ["cacheSettings"] = new JObject {
          ["enabled"] = hostState.CacheEnabled,
          ["directory"] = hostState.CacheDirectory ?? "",
        },
      };
      if (!_server.IsRunning && _server.StoppedReason != null) {
        state["stoppedReason"] = _server.StoppedReason;
      }
      if (_server.ServerVersion != null) {
        state["serverVersion"] = _server.ServerVersion;
      }
      if (hostState.LogDirectory != null) {
        state["logDirectory"] = hostState.LogDirectory;
      }
      if (hostState.Labels != null && hostState.Labels.Count > 0) {
        var labels = new JObject();
        foreach (var label in hostState.Labels) {
          labels[label.Key] = label.Value;
        }
        state["labels"] = labels;
      }
      Post(new JObject { ["type"] = "hostState", ["state"] = state });
    }

    /// <summary>Tells the page whether it is visible (it refreshes only while visible).</summary>
    public void PostVisibility(bool visible) {
      Post(new JObject { ["type"] = "visibility", ["visible"] = visible });
    }

    /// <summary>Sends the colors and fonts of the host (the "--vscode-*" variables).</summary>
    public void PostTheme(IDictionary<string, string> variables) {
      var values = new JObject();
      foreach (var variable in variables) {
        values[variable.Key] = variable.Value;
      }
      Post(new JObject { ["type"] = "theme", ["variables"] = values });
    }

    private void ServerOnNotification(string method, JToken parameters) {
      Post(new JObject {
        ["type"] = "notification",
        ["method"] = method,
        ["params"] = parameters ?? JValue.CreateNull(),
      });
    }

    private void ServerOnStateChanged(object sender, EventArgs e) {
      PostHostState();
    }

    private void Post(JObject message) {
      if (_disposed) {
        return;
      }
      _host.PostMessage(message.ToString(Formatting.None));
    }
  }
}
