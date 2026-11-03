// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Server.ProgressTracking {
  public class IndeterminateProgressTracker : ProgressTrackerBase {
    public IndeterminateProgressTracker(IProgressEventSender progressEventSender)
      : base(progressEventSender) {
    }

    public override int TotalStepCount { get { return int.MaxValue; } }
  }
}
