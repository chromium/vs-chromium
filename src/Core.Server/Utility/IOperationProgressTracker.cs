// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Utility {
  public interface IOperationProgressTracker {
    bool ShouldEndProcessing { get; }
    int ResultCount { get; }

    void AddResults(int count);
  }
}