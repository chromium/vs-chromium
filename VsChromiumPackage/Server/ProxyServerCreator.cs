// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using VsChromiumCore.Processes;

namespace VsChromiumPackage.Server {
  public delegate IEnumerable<string> PreCreate();

  public delegate void PostCreate(ProcessProxy processProxy);

  public interface IProxyServerCreator : IDisposable {
    void CreateProxy(PreCreate preCreate, PostCreate postCreate);
  }

  [Export(typeof(IProxyServerCreator))]
  public class ProxyServerCreator : IProxyServerCreator {
    private const string _proxyServerName = "VsChromiumHost.exe";
    private const string _serverName = "VsChromiumServer.exe";

    private readonly IProcessCreator _processCreator;
    private readonly object _serverProcessLock = new object();
    private ProcessProxy _serverProcess;

    [ImportingConstructor]
    public ProxyServerCreator(IProcessCreator processCreator) {
      this._processCreator = processCreator;
    }

    public void CreateProxy(PreCreate preCreate, PostCreate postCreate) {
      if (this._serverProcess == null) {
        lock (this._serverProcessLock) {
          if (this._serverProcess == null) {
            CreateServerProcessWorker(preCreate, postCreate);
          }
        }
      }
    }

    public void Dispose() {
      if (this._serverProcess != null) {
        this._serverProcess.Dispose();
      }
    }

    private void CreateServerProcessWorker(PreCreate preCreate, PostCreate postCreate) {
      var path = GetProcessPath();
      var realServerName = Path.Combine(Path.GetDirectoryName(path), _serverName);

      var arguments = new List<string>();
      arguments.Add(realServerName);
      arguments.AddRange(preCreate());
#if PROFILE_SERVER
      arguments.Add("/profile-server");
#endif

      var argumentLine = arguments.Aggregate("", (x, v) => x + QuoteArgument(v) + " ");
      this._serverProcess = this._processCreator.CreateProcess(path, argumentLine,
          CreateProcessOptions.RedirectStdio | CreateProcessOptions.AttachDebugger |
              CreateProcessOptions.BreakAwayFromJob);

      postCreate(this._serverProcess);
    }

    private string QuoteArgument(string argument) {
      const char quote = '\"';
      bool hasQuotes = argument.Length >= 2 && argument.First() == quote && argument.Last() == quote;
      if (hasQuotes)
        return argument;

      return quote + argument + quote;
    }

    private string GetProcessPath() {
      var result = GetCandidateProcessPaths()
          .Where(x => File.Exists(x))
          .OrderByDescending(x => File.GetLastWriteTimeUtc(x))
          .First();

      return result;
    }

    private IEnumerable<string> GetCandidateProcessPaths() {
      var folder = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      yield return Path.Combine(folder, _proxyServerName);

      var serverFolder = Path.Combine(folder, "..");
      serverFolder = Path.Combine(serverFolder, "..");
      serverFolder = Path.Combine(serverFolder, _serverName);

      yield return Path.Combine(serverFolder, "bin\\Debug");
      yield return Path.Combine(serverFolder, "bin\\Release");
    }
  }
}
