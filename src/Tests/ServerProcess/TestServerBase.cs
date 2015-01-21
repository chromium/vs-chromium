// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Logging;
using VsChromium.ServerProxy;

namespace VsChromium.Tests.ServerProcess {
  public abstract class TestServerBase : MefTestBase {
    // When using interactive debugger:
    //protected static readonly TimeSpan ServerResponseTimeout = TimeSpan.FromSeconds(5000.0);
    protected static readonly TimeSpan ServerResponseTimeout = TimeSpan.FromSeconds(5.0);

    protected DirectoryInfo GetChromiumEnlistmentDirectory() {
      var assemblyFileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
      var testDataPath = Path.Combine(assemblyFileInfo.Directory.Parent.Parent.FullName, "src", "Tests", "TestData", "src");
      var result = new DirectoryInfo(testDataPath);
      Assert.IsTrue(result.Exists, string.Format("Test data path \"{0}\" not found!", testDataPath));
      return result;
    }

    protected FileInfo GetChromiumEnlistmentFile() {
#if REAL_ENLISTMENT_TEST
      var filePath = @"D:\src\chromium\head\src\PRESUBMIT.py";
#else
      var filePath = Path.Combine(GetChromiumEnlistmentDirectory().FullName, "PRESUBMIT.py");
      var result = new FileInfo(filePath);
      Assert.IsTrue(result.Exists, string.Format("Test data file \"{0}\" not found!", filePath));
      return result;
#endif
    }

    protected FileSystemTree GetFileSystemFromServer(ITypedRequestProcessProxy server, FileInfo testFile) {
      SendFileSystemRequest(server, testFile, SendRegisterFileRequest);
      var tree = SendGetFileSystemRequest(server);

      // Entry under "Root" is the chromium enlistment entry
      Assert.IsTrue(tree.Root.Entries.Count == 1);
      var chromiumEntry = tree.Root.Entries[0] as DirectoryEntry;
      Assert.IsNotNull(chromiumEntry);
      Assert.AreEqual(testFile.DirectoryName, chromiumEntry.Name);

      return tree;
    }

    protected FileSystemTree SendGetFileSystemRequest(ITypedRequestProcessProxy server) {
      var response = SendRequest<GetFileSystemResponse>(server, new GetFileSystemRequest {
        KnownVersion = -1
      }, ServerResponseTimeout)();
      Assert.IsNotNull(response, "Server did not respond within timeout.");
      Assert.IsNotNull(response.Tree);
      Assert.IsNotNull(response.Tree.Root);
      Assert.IsNotNull(response.Tree.Root.Entries);

      return response.Tree;
    }

    protected delegate Func<DoneResponse> RequestProcessor(ITypedRequestProcessProxy server, FileInfo filename, TimeSpan timeout);

    protected void SendFileSystemRequest(ITypedRequestProcessProxy server, FileInfo testFile, RequestProcessor requestProcessor) {
      // Handle used to wait for "SearchEngineFilesLoaded" event.
      var filesLoadedEvent = new ManualResetEvent(false);
      Action<TypedEvent> eventHandler = @event => {
        if (@event is SearchEngineFilesLoaded) {
          filesLoadedEvent.Set();
        }
      };

      server.EventReceived += eventHandler;
      try {
        // Send request and wait for response.
        {
          var response = requestProcessor(server, testFile, ServerResponseTimeout)();
          Assert.IsNotNull(response, "Server did not respond within timeout.");
        }

        Assert.IsTrue(filesLoadedEvent.WaitOne(ServerResponseTimeout),
          "Server did not compute new file system tree within timeout.");
      }
      finally {
        server.EventReceived -= eventHandler;
      }
    }

    protected static Func<DoneResponse> SendRegisterFileRequest(ITypedRequestProcessProxy server, FileInfo filename, TimeSpan timeout) {
      var request = new RegisterFileRequest {
        FileName = filename.FullName
      };
      return SendRequest<DoneResponse>(server, request, timeout);
    }

    protected static Func<DoneResponse> SendUnregisterFileRequest(
      ITypedRequestProcessProxy server,
      FileInfo filename,
      TimeSpan timeout) {
      var request = new UnregisterFileRequest {
        FileName = filename.FullName
      };
      return SendRequest<DoneResponse>(server, request, timeout);
    }

    protected static Func<T> SendRequest<T>(ITypedRequestProcessProxy server, TypedRequest request, TimeSpan timeout)
      where T : TypedResponse {
      var sw = new Stopwatch();
      var waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
      T response = null;
      ErrorResponse error = null;

      sw.Start();
      server.RunAsync(request, typedResponse => {
        Assert.IsInstanceOfType(typedResponse, typeof(T));
        sw.Stop();
        Logger.Log("Request {0} took {1} msec to complete.", request.ClassName, sw.ElapsedMilliseconds);
        response = (T)typedResponse;
        waitHandle.Set();
      }, errorResponse => {
        error = errorResponse;
      });

      return () => {
        if (!waitHandle.WaitOne(timeout))
          return null;
        if (error != null)
          throw ErrorResponseHelper.CreateException(error);
        return response;
      };
    }
  }
}
