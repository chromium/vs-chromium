// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;

namespace VsChromium.Threads {
  [Export(typeof(IUIDelayedOperationProcessor))]
  public class UIDelayedOperationProcessor : IUIDelayedOperationProcessor {
    private readonly IDelayedOperationProcessor _delayedOperationProcessor;
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;

    [ImportingConstructor]
    public UIDelayedOperationProcessor(
      IDelayedOperationProcessor delayedOperationProcessor,
      ISynchronizationContextProvider synchronizationContextProvider) {
      _delayedOperationProcessor = delayedOperationProcessor;
      _synchronizationContextProvider = synchronizationContextProvider;
    }

    public void Post(DelayedOperation operation) {
      var action = operation.Action;
      operation.Action = () =>
        _synchronizationContextProvider.UIContext.Post(action);
      _delayedOperationProcessor.Post(operation);
    }
  }
}