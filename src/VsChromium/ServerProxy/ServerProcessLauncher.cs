// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Core.Processes;
using VsChromium.Package;

namespace VsChromium.ServerProxy {
  [Export(typeof(IServerProcessLauncher))]
  [Export(typeof(IPackagePostDispose))]
  public class ServerProcessLauncher : IServerProcessLauncher, IPackagePostDispose {
    private const string ProxyServerName = "VsChromium.Host.exe";
    private const string ServerName = "VsChromium.Server.exe";

    private readonly IProcessCreator _processCreator;
    private readonly IFileSystem _fileSystem;
    private readonly object _serverProcessLock = new object();
    private readonly CancellationTokenSource _processStartTokenSource = new CancellationTokenSource();
    private Task<CreateProcessResult> _startServerTask;

    [ImportingConstructor]
    public ServerProcessLauncher(IProcessCreator processCreator, IFileSystem fileSystem) {
      _processCreator = processCreator;
      _fileSystem = fileSystem;
    }

    public Task<CreateProcessResult> CreateProxyAsync(IList<string> arguments) {
      if (_startServerTask == null) {
        lock (_serverProcessLock) {
          if (_startServerTask == null) {
            // Create the process on the thread pool
            _startServerTask = Task.Run(() =>
                CreateServerProcessWorkerTask(arguments, _processStartTokenSource.Token));
          }
        }
      }
      return _startServerTask;
    }

    public void Dispose() {
      _processStartTokenSource.Cancel();
      _startServerTask?.Dispose();
    }

    private CreateProcessResult CreateServerProcessWorkerTask(IList<string> args, CancellationToken token) {
      token.ThrowIfCancellationRequested();

      Logger.LogInfo("Creating VsChromiumHost process.");
      var path = GetProcessPath();
      Logger.LogInfo("  Path={0}", path);
      var serverPath = PathHelpers.CombinePaths(PathHelpers.GetParent(path.Value), ServerName);
      Logger.LogInfo("  Server path={0}", serverPath);

      var arguments = new List<string>();
      arguments.Add(serverPath);
      arguments.AddRange(args);
#if PROFILE_SERVER
      arguments.Add("/profile-server");
#endif

      var argumentLine = arguments.Aggregate("", (x, v) => x + QuoteArgument(v) + " ");
      Logger.LogInfo("  Arguments={0}", argumentLine);
      var result = _processCreator.CreateProcess(path.Value, argumentLine,
                                                     CreateProcessOptions.AttachDebugger |
                                                     CreateProcessOptions.BreakAwayFromJob);
      Logger.LogInfo("VsChromiumHost process created (pid={0}).", result.Process.Id);

      token.ThrowIfCancellationRequested();
      return result;
    }

    private string QuoteArgument(string argument) {
      const char quote = '\"';
      bool hasQuotes = argument.Length >= 2 && argument.First() == quote && argument.Last() == quote;
      if (hasQuotes)
        return argument;

      return quote + argument + quote;
    }

    private FullPath GetProcessPath() {
      var result = GetCandidateProcessPaths()
        .Where(x => _fileSystem.FileExists(x))
        .OrderByDescending(x => _fileSystem.GetFileLastWriteTimeUtc(x))
        .First();

      return result;
    }

    private IEnumerable<FullPath> GetCandidateProcessPaths() {
      var folder = new FullPath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
      yield return folder.Combine(new RelativePath(ProxyServerName));

      var serverFolder = folder.Parent.Parent;

      yield return serverFolder.Combine(new RelativePath("bin\\Debug")).Combine(new RelativePath(ServerName));
      yield return serverFolder.Combine(new RelativePath("bin\\Release")).Combine(new RelativePath(ServerName));
    }

    int IPackagePostDispose.Priority => 0;

    void IPackagePostDispose.Run(IVisualStudioPackage package) {
      Dispose();
    }
  }
}
