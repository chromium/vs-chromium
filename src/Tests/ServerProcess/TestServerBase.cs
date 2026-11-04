// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Logging;
using VsChromium.Protocol.Generated;
using VsChromium.ServerProxy;

// Tests wait synchronously for the responses of the server (thread pool tasks,
// no UI thread involved).
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits

namespace VsChromium.Tests.ServerProcess {
  /// <summary>
  /// Base class of the tests running the real server (<c>--jsonrpc</c>)
  /// through <see cref="IServerClient"/>.
  /// </summary>
  public abstract class TestServerBase : MefTestBase {
    static TestServerBase() {
      // The servers started by these tests (child processes) don't use the index cache: not the cache of
      // the user (default directory), and a single "index/loaded" notification per load (a load from the
      // cache is followed by a second one, after its verification).
      Environment.SetEnvironmentVariable("VSCHROMIUM_CACHE_DIR", "none");
    }

    // When using interactive debugger:
    //protected static readonly TimeSpan ServerResponseTimeout = TimeSpan.FromSeconds(5000.0);
    protected static readonly TimeSpan ServerResponseTimeout = TimeSpan.FromSeconds(5.0);

    protected static FileSystemTree GetFileSystemFromServer(IServerClient server, FileInfo testFile) {
      SendFileSystemRequest(server, testFile, SendRegisterFileRequest);
      var tree = SendGetFileSystemTreeRequest(server);

      // The only project is the chromium enlistment
      Assert.AreEqual(1, tree.Projects.Count);
      Assert.AreEqual(testFile.DirectoryName, tree.Projects[0].RootPath);

      return tree;
    }

    protected static FileSystemTree SendGetFileSystemTreeRequest(IServerClient server) {
      var tree = SendRequest(server, client => client.TreeGetAsync(new TreeParams()));
      Assert.IsNotNull(tree);
      Assert.IsNotNull(tree.Projects);
      return tree;
    }

    protected delegate void RequestProcessor(IServerClient server, FileInfo filename);

    /// <summary>
    /// Sends a request changing the projects of the server, and waits for the
    /// server to load the new file contents ("index/loaded").
    /// </summary>
    protected static void SendFileSystemRequest(IServerClient server, FileInfo testFile, RequestProcessor requestProcessor) {
      // Handle used to wait for "index/loaded" notification.
      var filesLoadedEvent = new ManualResetEvent(false);
      Action<FilesLoadedParams> eventHandler = parameters => filesLoadedEvent.Set();

      server.IndexLoaded += eventHandler;
      try {
        // Send request and wait for response.
        requestProcessor(server, testFile);

        Assert.IsTrue(filesLoadedEvent.WaitOne(ServerResponseTimeout),
          "Server did not compute new file system tree within timeout.");
      }
      finally {
        server.IndexLoaded -= eventHandler;
      }
    }

    protected static void SendRegisterFileRequest(IServerClient server, FileInfo filename) {
      SendRequest(server, client => client.FilesRegisterAsync(new FilesRegisterParams {
        Path = filename.FullName
      }).WithNullResultAsync());
    }

    protected static void SendUnregisterFileRequest(IServerClient server, FileInfo filename) {
      SendRequest(server, client => client.FilesUnregisterAsync(new FilesUnregisterParams {
        Path = filename.FullName
      }).WithNullResultAsync());
    }

    /// <summary>
    /// Sends a request and waits (at most <see cref="ServerResponseTimeout"/>)
    /// for its response. Errors are thrown as <see cref="ServerErrorException"/>.
    /// </summary>
    protected static T SendRequest<T>(IServerClient server, Func<ProtocolClient, Task<T>> request) {
      var sw = Stopwatch.StartNew();
      var task = Task.Run(async () => {
        var client = await server.GetProtocolClientAsync().ConfigureAwait(false);
        return await request(client).ConfigureAwait(false);
      });
      try {
        Assert.IsTrue(task.Wait(ServerResponseTimeout), "Server did not respond within timeout.");
      }
      catch (AggregateException e) {
        throw ServerError.FromException(e).CreateException();
      }
      Logger.LogInfo("Request took {0} msec to complete.", sw.ElapsedMilliseconds);
      return task.Result;
    }
  }
}
