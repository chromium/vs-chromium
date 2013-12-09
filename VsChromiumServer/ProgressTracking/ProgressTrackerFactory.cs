// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumServer.Ipc.TypedEvents;

namespace VsChromiumServer.ProgressTracking {
  [Export(typeof(IProgressTrackerFactory))]
  public class ProgressTrackerFactory : IProgressTrackerFactory {
    private readonly ITypedEventSender _typedEventSender;

    [ImportingConstructor]
    public ProgressTrackerFactory(ITypedEventSender typedEventSender, IOperationIdFactory operationIdFactory) {
      this._typedEventSender = typedEventSender;
    }

    public IProgressTracker CreateTracker(int totalStepCount) {
      return new ProgressTracker(this._typedEventSender, totalStepCount);
    }

    public IProgressTracker CreateIndeterminateTracker() {
      return new IndeterminateProgressTracker(this._typedEventSender);
    }
  }
}
