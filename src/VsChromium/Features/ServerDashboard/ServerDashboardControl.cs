// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using VsChromium.Core.Logging;
using VsChromium.ServerProxy;
using VsChromium.Settings;

namespace VsChromium.Features.ServerDashboard {
  /// <summary>
  /// The content of the "VsChromium Server" tool window: the server dashboard
  /// page of the VS Code extension hosted in a WebView2 control. The page is
  /// served from the "Dashboard" folder of the extension (virtual host
  /// <see cref="VirtualHostName"/>), with a strict Content Security Policy
  /// (dashboard.html); it talks to Visual Studio only through web messages
  /// (<see cref="DashboardBridge"/>).
  /// </summary>
  public class ServerDashboardControl : UserControl, IDisposable {
    /// <summary>The host name of the page (a reserved top level domain: never resolved on the network).</summary>
    public const string VirtualHostName = "dashboard.vschromium.example";
    public const string PageUrl = "https://" + VirtualHostName + "/dashboard.html";

    private readonly Grid _grid = new Grid();
    private readonly TextBlock _message = new TextBlock {
      Margin = new Thickness(12),
      TextWrapping = TextWrapping.Wrap,
    };
    private WebView2 _webView;
    private DashboardBridge _bridge;
    private DashboardServerAdapter _server;
    private bool _pageReady;
    private bool _disposed;

    public ServerDashboardControl() {
      _message.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
      _grid.SetResourceReference(BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
      _message.Text = "Loading the server dashboard\u2026";
      _grid.Children.Add(_message);
      Content = _grid;
      IsVisibleChanged += (sender, args) => PostVisibility();
    }

    /// <summary>The folder of the page bundle (dashboard.html, dashboard.js, dashboard.css), next to the package assembly.</summary>
    public static string PageFolder =>
      Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "Dashboard");

    /// <summary>The user data folder of WebView2 (not next to the extension: per user, writable).</summary>
    public static string UserDataFolder =>
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VsChromium", "WebView2");

    public void OnToolWindowCreated(IServiceProvider serviceProvider) {
      InitializeAsync(serviceProvider).FileAndForget("VsChromium/ServerDashboard/Initialize");
    }

    private async Task InitializeAsync(IServiceProvider serviceProvider) {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      if (!File.Exists(Path.Combine(PageFolder, "dashboard.js"))) {
        ShowMessage(string.Format("The server dashboard page is missing from the extension ({0}). " +
                                  "Build the VS Code extension (src\\vscode: npm install, npm run compile) before the Visual Studio extension.",
                                  PageFolder));
        return;
      }
      try {
        CoreWebView2Environment.GetAvailableBrowserVersionString();
      }
      catch (WebView2RuntimeNotFoundException) {
        ShowMessage("The server dashboard needs the Microsoft Edge WebView2 Runtime (https://go.microsoft.com/fwlink/p/?LinkId=2124703).");
        return;
      }

      var componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
      var exports = componentModel.DefaultExportProvider;
      var serverClient = exports.GetExportedValue<IServerClient>();
      _server = new DashboardServerAdapter(serverClient);
      var host = new VsDashboardHost(serverClient, exports.GetExportedValue<IIndexServerSettingsProvider>(),
        exports.GetExportedValue<IProjectOptionsService>(), serviceProvider, PostMessage);
      _bridge = new DashboardBridge(_server, host);
      _bridge.PageReady += (sender, args) => {
        _pageReady = true;
        PostTheme();
        PostVisibility();
      };

      try {
        Directory.CreateDirectory(UserDataFolder);
        var environment = await CoreWebView2Environment.CreateAsync(null, UserDataFolder);
        _webView = new WebView2();
        _grid.Children.Add(_webView);
        await _webView.EnsureCoreWebView2Async(environment);
        var core = _webView.CoreWebView2;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = true;
#if !DEBUG
        core.Settings.AreDevToolsEnabled = false;
#endif
        core.SetVirtualHostNameToFolderMapping(VirtualHostName, PageFolder, CoreWebView2HostResourceAccessKind.Deny);
        // Note: The page never navigates: other navigations (e.g. links) are blocked.
        core.NavigationStarting += (sender, args) => {
          if (!args.Uri.StartsWith("https://" + VirtualHostName + "/", StringComparison.OrdinalIgnoreCase)) {
            args.Cancel = true;
          }
        };
        core.NewWindowRequested += (sender, args) => args.Handled = true;
        core.WebMessageReceived += CoreOnWebMessageReceived;
        VSColorTheme.ThemeChanged += VsColorThemeOnThemeChanged;
        _grid.Children.Remove(_message);
        core.Navigate(PageUrl);
      }
      catch (Exception e) {
        Logger.LogError(e, "Error creating the server dashboard.");
        ShowMessage("Error creating the server dashboard: " + e.Message);
      }
    }

    private void CoreOnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e) {
      if (!e.Source.StartsWith("https://" + VirtualHostName + "/", StringComparison.OrdinalIgnoreCase)) {
        return;
      }
      var json = e.WebMessageAsJson;
      _bridge?.HandleMessageAsync(json).FileAndForget("VsChromium/ServerDashboard/Message");
    }

    private void VsColorThemeOnThemeChanged(ThemeChangedEventArgs e) {
      PostTheme();
    }

    /// <summary>Sends a message to the page (any thread).</summary>
    private void PostMessage(string json) {
      PostMessageAsync(json).FileAndForget("VsChromium/ServerDashboard/PostMessage");
    }

    private async Task PostMessageAsync(string json) {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      if (!_disposed && _webView?.CoreWebView2 != null) {
        _webView.CoreWebView2.PostWebMessageAsJson(json);
      }
    }

    private void PostTheme() {
      ThreadHelper.ThrowIfNotOnUIThread();
      if (_bridge == null || !_pageReady) {
        return;
      }
      _bridge.PostTheme(DashboardTheme.BuildVariables(VsDashboardTheme.GetCurrentTheme(this)));
    }

    private void PostVisibility() {
      if (_bridge == null || !_pageReady) {
        return;
      }
      _bridge.PostVisibility(IsVisible);
    }

    private void ShowMessage(string text) {
      _message.Text = text;
      if (!_grid.Children.Contains(_message)) {
        _grid.Children.Add(_message);
      }
    }

    public void Dispose() {
      if (_disposed) {
        return;
      }
      _disposed = true;
      VSColorTheme.ThemeChanged -= VsColorThemeOnThemeChanged;
      _bridge?.Dispose();
      _server?.Dispose();
      _webView?.Dispose();
    }
  }
}
