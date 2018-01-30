// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;

namespace VsChromium.Threads {
  [Export(typeof(IDispatchThreadDelayedOperationExecutor))]
  public class DispatchThreadDelayedOperationExecutor : IDispatchThreadDelayedOperationExecutor {
    private readonly IDelayedOperationExecutor _delayedOperationExecutor;
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;

    [ImportingConstructor]
    public DispatchThreadDelayedOperationExecutor(
      IDelayedOperationExecutor delayedOperationExecutor,
      ISynchronizationContextProvider synchronizationContextProvider) {
      _delayedOperationExecutor = delayedOperationExecutor;
      _synchronizationContextProvider = synchronizationContextProvider;
    }

    public void Post(DelayedOperation operation) {
      var action = operation.Action;
      operation.Action = () =>
        _synchronizationContextProvider.UIContext.Post(action);
      _delayedOperationExecutor.Post(operation);
    }
  }
}