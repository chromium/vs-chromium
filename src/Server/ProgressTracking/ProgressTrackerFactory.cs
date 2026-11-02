// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.Ipc.TypedEvents;

namespace VsChromium.Server.ProgressTracking {
  public class ProgressTrackerFactory : IProgressTrackerFactory {
    private readonly ITypedEventSender _typedEventSender;

    public ProgressTrackerFactory(ITypedEventSender typedEventSender) {
      _typedEventSender = typedEventSender;
    }

    public IProgressTracker CreateTracker(int totalStepCount) {
      return new ProgressTracker(_typedEventSender, totalStepCount);
    }

    public IProgressTracker CreateIndeterminateTracker() {
      return new IndeterminateProgressTracker(_typedEventSender);
    }
  }
}
