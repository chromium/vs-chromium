// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using VsChromium.Features.ServerDashboard;

namespace VsChromium.Tests.VsChromium.ServerDashboard {
  [TestClass]
  public class TestDashboardBridge {
    private class FakeServer : IDashboardServer {
      public readonly List<Tuple<string, JToken>> Requests = new List<Tuple<string, JToken>>();
      public Func<string, JToken, JToken> Handler = (method, parameters) => new JObject { ["ok"] = true };

      public bool IsRunning { get; set; } = true;
      public string ServerVersion { get; set; } = "1.2.3";
      public string StoppedReason { get; set; }

      public Task<JToken> InvokeAsync(string method, JToken parameters, CancellationToken cancellationToken) {
        Requests.Add(Tuple.Create(method, parameters));
        return Task.FromResult(Handler(method, parameters));
      }

      public event Action<string, JToken> Notification;
      public event EventHandler StateChanged;

      public void Notify(string method, JToken parameters) {
        Notification?.Invoke(method, parameters);
      }

      public void ChangeState(bool running, string reason = null) {
        IsRunning = running;
        StoppedReason = reason;
        StateChanged?.Invoke(this, EventArgs.Empty);
      }
    }

    private class FakeHost : IDashboardHost {
      public readonly List<JObject> Posted = new List<JObject>();
      public readonly List<Tuple<string, JObject>> Commands = new List<Tuple<string, JObject>>();
      public DashboardHostState State = new DashboardHostState {
        TrigramFiltersSetting = false,
        CacheEnabled = true,
        CacheDirectory = "",
        LogDirectory = "C:\\logs",
        Labels = new Dictionary<string, string> { ["trigramFiltersDefault"] = "Tools > Options" },
      };

      public void PostMessage(string json) {
        Posted.Add(JObject.Parse(json));
      }

      public DashboardHostState GetHostState() {
        return State;
      }

      public Task ExecuteCommandAsync(string command, JObject message) {
        Commands.Add(Tuple.Create(command, message));
        if (command == "setTrigramFiltersSetting") {
          State.TrigramFiltersSetting = (bool)message["enabled"];
        }
        return Task.CompletedTask;
      }

      public string GetErrorMessage(Exception error) {
        return "error: " + error.Message;
      }

      public IEnumerable<JObject> OfType(string type) {
        return Posted.Where(m => (string)m["type"] == type);
      }
    }

    private static Tuple<DashboardBridge, FakeServer, FakeHost> Setup() {
      var server = new FakeServer();
      var host = new FakeHost();
      return Tuple.Create(new DashboardBridge(server, host), server, host);
    }

    [TestMethod]
    public async Task ReadySendsTheHostStateAndRaisesPageReadyAsync() {
      var (bridge, server, host) = Setup();
      var ready = 0;
      bridge.PageReady += (sender, args) => ready++;
      await bridge.HandleMessageAsync("{\"type\":\"ready\"}");
      Assert.AreEqual(1, ready);
      var state = (JObject)host.OfType("hostState").Single()["state"];
      Assert.AreEqual("Visual Studio", (string)state["hostName"]);
      Assert.AreEqual(true, (bool)state["serverRunning"]);
      Assert.AreEqual("1.2.3", (string)state["serverVersion"]);
      Assert.AreEqual(false, (bool)state["trigramFiltersSetting"]);
      Assert.AreEqual("C:\\logs", (string)state["logDirectory"]);
      Assert.AreEqual(true, (bool)state["cacheSettings"]["enabled"]);
      Assert.AreEqual("", (string)state["cacheSettings"]["directory"]);
      Assert.AreEqual("Tools > Options", (string)state["labels"]["trigramFiltersDefault"]);
      Assert.IsNull(state["stoppedReason"]);
    }

    [TestMethod]
    public async Task ForwardsTheAllowedRequestsWithTheirRawParametersAsync() {
      var (bridge, server, host) = Setup();
      server.Handler = (method, parameters) => method == "index/status" ? (JToken)new JObject { ["status"] = "idle" } : null;
      await bridge.HandleMessageAsync("{\"type\":\"request\",\"id\":7,\"method\":\"index/status\"}");
      await bridge.HandleMessageAsync("{\"type\":\"request\",\"id\":8,\"method\":\"server/history\",\"params\":{\"sinceUnixMs\":5}}");
      await bridge.HandleMessageAsync("{\"type\":\"request\",\"id\":9,\"method\":\"project/rebuild\",\"params\":{\"projectPath\":\"C:\\\\a\"}}");
      await bridge.HandleMessageAsync("{\"type\":\"request\",\"id\":10,\"method\":\"project/directorySizes\",\"params\":{\"projectPath\":\"C:\\\\a\",\"relativePath\":\"src\"}}");

      Assert.AreEqual(4, server.Requests.Count);
      Assert.AreEqual("index/status", server.Requests[0].Item1);
      Assert.IsNull(server.Requests[0].Item2, "no parameters");
      Assert.AreEqual("server/history", server.Requests[1].Item1);
      Assert.AreEqual(5, (int)server.Requests[1].Item2["sinceUnixMs"]);
      Assert.AreEqual("C:\\a", (string)server.Requests[2].Item2["projectPath"]);
      Assert.AreEqual("project/directorySizes", server.Requests[3].Item1);
      Assert.AreEqual("src", (string)server.Requests[3].Item2["relativePath"]);

      var responses = host.OfType("response").ToList();
      Assert.AreEqual(7, (int)responses[0]["id"]);
      Assert.AreEqual("idle", (string)responses[0]["result"]["status"]);
      Assert.AreEqual(JTokenType.Null, responses[1]["result"].Type, "null results are sent as null");
      Assert.IsNull(responses[0]["error"]);
    }

    [TestMethod]
    public async Task RejectsTheMethodsNotUsedByTheDashboardAsync() {
      var (bridge, server, host) = Setup();
      await bridge.HandleMessageAsync("{\"type\":\"request\",\"id\":1,\"method\":\"search/code\",\"params\":{}}");
      await bridge.HandleMessageAsync("{\"type\":\"request\",\"id\":2,\"method\":\"files/register\",\"params\":{}}");
      await bridge.HandleMessageAsync("{\"type\":\"request\",\"id\":3,\"method\":\"project/addIgnorePattern\",\"params\":{}}");
      Assert.AreEqual(0, server.Requests.Count);
      var responses = host.OfType("response").ToList();
      Assert.AreEqual("Request not allowed: search/code", (string)responses[0]["error"]);
      Assert.AreEqual("Request not allowed: files/register", (string)responses[1]["error"]);
      Assert.AreEqual("Request not allowed: project/addIgnorePattern", (string)responses[2]["error"]);
    }

    [TestMethod]
    public void TheAllowListAndCommandsMatchTheTypeScriptDashboardMessages() {
      var messagesPath = Path.Combine(RepoRootDirectory(), "src", "vscode", "src", "ui", "dashboard", "dashboardMessages.ts");
      var ts = File.ReadAllText(messagesPath);

      var methodsBlock = Regex.Match(ts, @"DashboardRequestMethods\s*=\s*\[(?<body>[^\]]+)\]");
      Assert.IsTrue(methodsBlock.Success, "DashboardRequestMethods not found in dashboardMessages.ts");
      var tsMethods = Regex.Matches(methodsBlock.Groups["body"].Value, @"'([^']+)'")
        .Cast<Match>()
        .Select(m => m.Groups[1].Value)
        .ToList();
      CollectionAssert.AreEquivalent(tsMethods, DashboardProtocol.RequestMethods.ToList());

      var tsCommands = Regex.Matches(ts, @"\{\s*command:\s*'([^']+)'")
        .Cast<Match>()
        .Select(m => m.Groups[1].Value)
        .Distinct()
        .ToList();
      CollectionAssert.AreEquivalent(tsCommands, DashboardProtocol.Commands.ToList());
    }

    [TestMethod]
    public async Task ReturnsTheErrorsOfTheServerAsync() {
      var (bridge, server, host) = Setup();
      server.Handler = (method, parameters) => { throw new RemoteInvocationException("Unknown project", -32001, (object)null); };
      await bridge.HandleMessageAsync("{\"type\":\"request\",\"id\":3,\"method\":\"project/details\",\"params\":{}}");
      Assert.AreEqual("error: Unknown project", (string)host.OfType("response").Single()["error"]);
    }

    [TestMethod]
    public async Task RequestsFailWhenTheServerIsNotRunningAsync() {
      var (bridge, server, host) = Setup();
      server.IsRunning = false;
      await bridge.HandleMessageAsync("{\"type\":\"request\",\"id\":3,\"method\":\"index/status\"}");
      Assert.AreEqual(0, server.Requests.Count);
      Assert.AreEqual("The indexing server is not running.", (string)host.OfType("response").Single()["error"]);
    }

    [TestMethod]
    public void ForwardsTheNotificationsAndTheStateOfTheServer() {
      var (bridge, server, host) = Setup();
      server.Notify("index/stateChanged", new JObject { ["status"] = "paused" });
      var notification = host.OfType("notification").Single();
      Assert.AreEqual("index/stateChanged", (string)notification["method"]);
      Assert.AreEqual("paused", (string)notification["params"]["status"]);

      server.ChangeState(false, "crashed");
      var state = (JObject)host.OfType("hostState").Last()["state"];
      Assert.AreEqual(false, (bool)state["serverRunning"]);
      Assert.AreEqual("crashed", (string)state["stoppedReason"]);
    }

    [TestMethod]
    public async Task ExecutesTheCommandsThroughTheHostAndSendsTheNewStateAsync() {
      var (bridge, server, host) = Setup();
      await bridge.HandleMessageAsync("{\"type\":\"command\",\"command\":\"setTrigramFiltersSetting\",\"enabled\":true}");
      await bridge.HandleMessageAsync("{\"type\":\"command\",\"command\":\"rebuildProject\",\"projectPath\":\"C:\\\\a\"}");
      await bridge.HandleMessageAsync("{\"type\":\"command\",\"command\":\"copyToClipboard\",\"text\":\"/out/\"}");
      await bridge.HandleMessageAsync("{\"type\":\"command\",\"command\":\"addIgnorePattern\",\"projectPath\":\"C:\\\\a\",\"section\":\"sourceExplorerIgnore\",\"pattern\":\"/out/\"}");
      await bridge.HandleMessageAsync("{\"type\":\"command\",\"command\":\"deleteEverything\"}");
      Assert.AreEqual(4, host.Commands.Count, "unknown commands are ignored");
      Assert.AreEqual("setTrigramFiltersSetting", host.Commands[0].Item1);
      Assert.AreEqual("C:\\a", (string)host.Commands[1].Item2["projectPath"]);
      Assert.AreEqual("copyToClipboard", host.Commands[2].Item1);
      Assert.AreEqual("/out/", (string)host.Commands[2].Item2["text"]);
      Assert.AreEqual("addIgnorePattern", host.Commands[3].Item1);
      Assert.AreEqual("sourceExplorerIgnore", (string)host.Commands[3].Item2["section"]);
      Assert.AreEqual("/out/", (string)host.Commands[3].Item2["pattern"]);
      Assert.AreEqual(0, server.Requests.Count, "the commands are executed by the host");
      var state = (JObject)host.OfType("hostState").First()["state"];
      Assert.AreEqual(true, (bool)state["trigramFiltersSetting"]);
    }

    [TestMethod]
    public void SendsTheVisibilityAndTheTheme() {
      var (bridge, server, host) = Setup();
      bridge.PostVisibility(false);
      bridge.PostTheme(new Dictionary<string, string> { ["--vscode-foreground"] = "#cccccc" });
      Assert.AreEqual(false, (bool)host.OfType("visibility").Single()["visible"]);
      Assert.AreEqual("#cccccc", (string)host.OfType("theme").Single()["variables"]["--vscode-foreground"]);
    }

    [TestMethod]
    public async Task IgnoresInvalidMessagesAndStopsWhenDisposedAsync() {
      var (bridge, server, host) = Setup();
      await bridge.HandleMessageAsync("not json");
      await bridge.HandleMessageAsync("{\"type\":\"request\",\"method\":\"index/status\"}");
      Assert.AreEqual(0, host.Posted.Count);
      bridge.Dispose();
      server.Notify("index/loaded", new JObject());
      Assert.AreEqual(0, host.Posted.Count);
    }

    private static string RepoRootDirectory() {
      var directory = new DirectoryInfo(Path.GetDirectoryName(typeof(TestDashboardBridge).Assembly.Location));
      while (directory != null && !File.Exists(Path.Combine(directory.FullName, "build.root"))) {
        directory = directory.Parent;
      }
      Assert.IsNotNull(directory, "build.root not found");
      return directory.FullName;
    }
  }
}
