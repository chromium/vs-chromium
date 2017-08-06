// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;

namespace VsChromium.Server.Operations {
  [Export(typeof(IOperationProcessor))]
  public class OperationProcessor : IOperationProcessor {
    private readonly IOperationIdFactory _operationIdFactory;

    [ImportingConstructor]
    public OperationProcessor(IOperationIdFactory operationIdFactory) {
      _operationIdFactory = operationIdFactory;
    }

    public void Execute(OperationHandlers operationHandlers) {
      var info = new OperationInfo {
        OperationId = _operationIdFactory.GetNextId()
      };
      operationHandlers.OnBeforeExecute(info);
      try {
        operationHandlers.Execute(info);
      }
      catch (Exception e) {
        if (!e.IsCanceled()) {
          Logger.LogError(e, "Error executing operation {0}", info.OperationId);
        }
        operationHandlers.OnError(info, e);
      }
    }
  }
}