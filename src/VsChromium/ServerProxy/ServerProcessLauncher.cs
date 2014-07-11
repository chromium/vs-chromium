// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using VsChromium.Core;
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
    private CreateProcessResult _serverProcess;

    [ImportingConstructor]
    public ServerProcessLauncher(IProcessCreator processCreator, IFileSystem fileSystem) {
      _processCreator = processCreator;
      _fileSystem = fileSystem;
    }

    public void CreateProxy(Func<IEnumerable<string>> preCreate, Action<CreateProcessResult> postCreate) {
      if (_serverProcess == null) {
        lock (_serverProcessLock) {
          if (_serverProcess == null) {
            CreateServerProcessWorker(preCreate, postCreate);
          }
        }
      }
    }

    public void Dispose() {
      if (_serverProcess != null) {
        _serverProcess.Dispose();
      }
    }

    private void CreateServerProcessWorker(Func<IEnumerable<string>> preCreate, Action<CreateProcessResult> postCreate) {
      Logger.Log("Creating VsChromiumHost process.");
      var path = GetProcessPath();
      Logger.Log("  Path={0}", path);
      var serverPath = Path.Combine(Path.GetDirectoryName(path.Value), ServerName);
      Logger.Log("  Server path={0}", serverPath);

      var arguments = new List<string>();
      arguments.Add(serverPath);
      arguments.AddRange(preCreate());
#if PROFILE_SERVER
      arguments.Add("/profile-server");
#endif

      var argumentLine = arguments.Aggregate("", (x, v) => x + QuoteArgument(v) + " ");
      Logger.Log("  Arguments={0}", argumentLine);
      _serverProcess = _processCreator.CreateProcess(path.Value, argumentLine,
                                                     CreateProcessOptions.AttachDebugger |
                                                     CreateProcessOptions.BreakAwayFromJob);
      Logger.Log("VsChromiumHost process created (pid={0}).", _serverProcess.Process.Id);
      postCreate(_serverProcess);
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

    public int Priority { get { return 0; } }

    public void Run(IVisualStudioPackage package) {
      this.Dispose();
    }
  }
}
