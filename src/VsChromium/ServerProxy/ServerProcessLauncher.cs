// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Core.Logging;
using VsChromium.Package;
using VsChromium.Settings;

namespace VsChromium.ServerProxy {
  /// <summary>
  /// Starts the indexing server (in the "Server" directory next to the package
  /// assemblies, both in the build output and in the VSIX: the server of the
  /// native architecture of Windows, see <see cref="ServerLocation"/>)
  /// directly, with the <c>--jsonrpc</c> argument: JSON-RPC messages are
  /// exchanged over the standard input/output of the server (see
  /// protocol/README.md), which exits when its standard input is closed (i.e.
  /// when Visual Studio exits or crashes). This is the same protocol as the VS
  /// Code extension.
  /// </summary>
  [Export(typeof(IServerProcessLauncher))]
  [Export(typeof(IPackagePostDispose))]
  public class ServerProcessLauncher : IServerProcessLauncher, IPackagePostDispose {
    private const string JsonRpcArgument = "--jsonrpc";
    private const string CacheDirectoryArgument = "--cache-dir";

    private readonly object _serverProcessLock = new object();
    private readonly CancellationTokenSource _processStartTokenSource = new CancellationTokenSource();
    private readonly IIndexServerSettingsProvider _settingsProvider;
    private Task<ServerProcess> _startServerTask;

    [ImportingConstructor]
    public ServerProcessLauncher(IIndexServerSettingsProvider settingsProvider) {
      _settingsProvider = settingsProvider;
    }

    public Task<ServerProcess> StartServerAsync() {
      lock (_serverProcessLock) {
        if (_startServerTask == null) {
          // Create the process on the thread pool
          var arguments = GetArguments(_settingsProvider.Settings);
          _startServerTask = Task.Run(() => StartServerWorker(arguments, _processStartTokenSource.Token));
        }
        return _startServerTask;
      }
    }

    public void StopServer() {
      Task<ServerProcess> task;
      lock (_serverProcessLock) {
        task = _startServerTask;
        _startServerTask = null;
      }
      if (task != null && task.Status == TaskStatus.RanToCompletion) {
        task.Result.Stop(TimeSpan.FromSeconds(2));
      }
    }

    /// <summary>
    /// The command line arguments of the server: JSON-RPC mode, and the cache
    /// directory of the "Index Server" options (if set).
    /// </summary>
    public static string GetArguments(IndexServerSettings settings) {
      var directory = (settings?.CacheDirectory ?? "").Trim();
      if (directory.Length == 0) {
        return JsonRpcArgument;
      }
      return string.Format("{0} {1} \"{2}\"", JsonRpcArgument, CacheDirectoryArgument,
        // Note: A trailing backslash would escape the closing quote.
        directory.EndsWith("\\") ? directory + "." : directory);
    }

    public void Dispose() {
      _processStartTokenSource.Cancel();
      var task = _startServerTask;
      if (task != null && task.Status == TaskStatus.RanToCompletion) {
        task.Result.Stop(TimeSpan.FromSeconds(2));
      }
    }

    private static ServerProcess StartServerWorker(string arguments, CancellationToken token) {
      token.ThrowIfCancellationRequested();

      var folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
      var nativeMachine = ServerLocation.GetNativeMachine();
      var runtimeIdentifier = ServerLocation.GetRuntimeIdentifier(nativeMachine, RuntimeInformation.OSArchitecture);
      var serverPath = ServerLocation.FindServerPath(folder, runtimeIdentifier, File.Exists);
      if (serverPath == null) {
        throw new FileNotFoundException(string.Format("The indexing server was not found in \"{0}\" (runtime identifier: {1}, tried: {2})",
          folder, runtimeIdentifier ?? "unknown", string.Join(", ", ServerLocation.GetCandidatePaths(runtimeIdentifier))));
      }
      Logger.LogInfo("Starting server process (native machine 0x{0:X4}, process {1}, server {2}): \"{3}\" {4}",
        nativeMachine, RuntimeInformation.ProcessArchitecture, runtimeIdentifier ?? "unknown", serverPath, arguments);

      var startInfo = new ProcessStartInfo(serverPath, arguments) {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WorkingDirectory = Path.GetDirectoryName(serverPath) ?? "",
      };
      var process = Process.Start(startInfo);
      if (process == null) {
        throw new InvalidOperationException(string.Format("Error starting server process \"{0}\"", serverPath));
      }
      Logger.LogInfo("Server process started (pid={0}).", process.Id);

      // Anything written to stderr by the server is logged (stdout is reserved
      // for messages).
      process.ErrorDataReceived += (sender, args) => {
        if (args.Data != null) {
          Logger.LogInfo("Server process stderr: {0}", args.Data);
        }
      };
      process.BeginErrorReadLine();

      token.ThrowIfCancellationRequested();
      return new ServerProcess(process);
    }

    int IPackagePostDispose.Priority => 0;

    void IPackagePostDispose.Run(IVisualStudioPackage package) {
      Dispose();
    }
  }
}
