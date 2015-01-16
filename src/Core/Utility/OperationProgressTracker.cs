// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Threading;

namespace VsChromium.Core.Utility {
  public class OperationProgressTracker : IOperationProgressTracker {
    private readonly TaskResultCounter _resultCounter;
    private readonly CancellationToken _cancellationToken;

    public OperationProgressTracker(int maxResults, CancellationToken cancellationToken) {
      _resultCounter = new TaskResultCounter(maxResults);
      _cancellationToken = cancellationToken;
    }

    public bool ShouldEndProcessing {
      get { return _resultCounter.Done || _cancellationToken.IsCancellationRequested; }
    }

    public int ResultCount {
      get { return _resultCounter.Count; }
    }

    public static IOperationProgressTracker None {
      get {
        return new OperationProgressTracker(int.MaxValue, CancellationToken.None);
      }
    }

    public void AddResults(int count) {
      _resultCounter.Add(count);
    }
  }
}