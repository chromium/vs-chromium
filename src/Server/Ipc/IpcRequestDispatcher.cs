// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using VsChromium.Core.Ipc;
using VsChromium.Core.Logging;
using VsChromium.Server.Ipc.ProtocolHandlers;
using VsChromium.Server.Threads;

namespace VsChromium.Server.Ipc {
  [Export(typeof(IIpcRequestDispatcher))]
  public class IpcRequestDispatcher : IIpcRequestDispatcher {
    private readonly ICustomThreadPool _customThreadPool;
    private readonly IIpcResponseQueue _ipcResponseQueue;
    private readonly IEnumerable<IProtocolHandler> _protocolHandlers;

    [ImportingConstructor]
    public IpcRequestDispatcher(
      ICustomThreadPool customThreadPool,
      IIpcResponseQueue ipcResponseQueue,
      [ImportMany] IEnumerable<IProtocolHandler> protocolHandlers) {
      _customThreadPool = customThreadPool;
      _ipcResponseQueue = ipcResponseQueue;
      _protocolHandlers = protocolHandlers;
    }

    public void ProcessRequestAsync(IpcRequest request) {
      _customThreadPool.RunAsync(() => ProcessRequestTask(request));
    }

    private void ProcessRequestTask(IpcRequest request) {
      var sw = Stopwatch.StartNew();
      var response = ProcessOneRequest(request);
      _ipcResponseQueue.Enqueue(response);
      sw.Stop();

      Logger.Log("Request {0} of type \"{1}\" took {2:n0} msec to handle.",
                 request.RequestId, request.Data.GetType().Name, sw.ElapsedMilliseconds);
    }

    private IpcResponse ProcessOneRequest(IpcRequest request) {
      try {
        var processor = _protocolHandlers.FirstOrDefault(x => x.CanProcess(request));
        if (processor == null) {
          throw new Exception(string.Format("Request protocol {0} is not recognized by any request processor!",
                                            request.Protocol));
        }

        return processor.Process(request);
      }
      catch (OperationCanceledException e) {
        Logger.Log("Request {0} of type \"{1}\" has been canceled.",
                   request.RequestId, request.Data.GetType().Name);
        return ErrorResponseHelper.CreateIpcErrorResponse(request, e);
      }
      catch (Exception e) {
        var message = string.Format("Error executing request {0} of type \"{1}\".",
                            request.RequestId, request.Data.GetType().Name);
        Logger.LogException(e, "{0}", message);
        var outer = new Exception(message, e);
        return ErrorResponseHelper.CreateIpcErrorResponse(request, outer);
      }
    }
  }
}
