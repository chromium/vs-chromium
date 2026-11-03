// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Server.ProgressTracking {
  public class ProgressTrackerFactory : IProgressTrackerFactory {
    private readonly IProgressEventSender _progressEventSender;

    public ProgressTrackerFactory(IProgressEventSender progressEventSender) {
      _progressEventSender = progressEventSender;
    }

    public IProgressTracker CreateTracker(int totalStepCount) {
      return new ProgressTracker(_progressEventSender, totalStepCount);
    }

    public IProgressTracker CreateIndeterminateTracker() {
      return new IndeterminateProgressTracker(_progressEventSender);
    }
  }
}
