// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Core.Ipc.Json;
using VsChromium.Core.Logging;
using VsChromium.Package;

namespace VsChromium.ServerProxy {
  /// <summary>
  /// Starts the indexing server (<c>Server\VsChromium.Server.exe</c>, next to
  /// the package assemblies, both in the build output and in the VSIX)
  /// directly, with the <c>--stdio</c> argument: messages are exchanged as JSON
  /// lines over the standard input/output of the server, which exits when its
  /// standard input is closed (i.e. when Visual Studio exits or crashes). This
  /// is the same protocol as the VS Code extension.
  /// </summary>
  [Export(typeof(IServerProcessLauncher))]
  [Export(typeof(IPackagePostDispose))]
  public class ServerProcessLauncher : IServerProcessLauncher, IPackagePostDispose {
    /// <summary>
    /// The server (modern .NET) is in a "Server" sub-directory of the
    /// directory containing the package assemblies.
    /// </summary>
    private const string ServerName = "Server\\VsChromium.Server.exe";
    private const string StdioArgument = "--stdio";

    private readonly object _serverProcessLock = new object();
    private readonly CancellationTokenSource _processStartTokenSource = new CancellationTokenSource();
    private Task<ServerProcess> _startServerTask;

    public Task<ServerProcess> StartServerAsync() {
      if (_startServerTask == null) {
        lock (_serverProcessLock) {
          if (_startServerTask == null) {
            // Create the process on the thread pool
            _startServerTask = Task.Run(() => StartServerWorker(_processStartTokenSource.Token));
          }
        }
      }
      return _startServerTask;
    }

    public void Dispose() {
      _processStartTokenSource.Cancel();
      var task = _startServerTask;
      if (task != null && task.Status == TaskStatus.RanToCompletion) {
        task.Result.Stop(TimeSpan.FromSeconds(2));
      }
    }

    private static ServerProcess StartServerWorker(CancellationToken token) {
      token.ThrowIfCancellationRequested();

      var folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      var serverPath = Path.Combine(folder ?? "", ServerName);
      Logger.LogInfo("Starting server process: \"{0}\" {1}", serverPath, StdioArgument);

      var startInfo = new ProcessStartInfo(serverPath, StdioArgument) {
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

      var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
      var reader = new StreamReader(process.StandardOutput.BaseStream, encoding);
      var writer = new StreamWriter(process.StandardInput.BaseStream, encoding) { AutoFlush = false };
      var stream = new IpcStreamOverJsonLines(reader, writer,
        (e, message) => Logger.LogError(e, "{0}", message),
        message => Logger.LogInfo("{0}", message));

      token.ThrowIfCancellationRequested();
      return new ServerProcess(process, stream, writer);
    }

    int IPackagePostDispose.Priority => 0;

    void IPackagePostDispose.Run(IVisualStudioPackage package) {
      Dispose();
    }
  }
}
