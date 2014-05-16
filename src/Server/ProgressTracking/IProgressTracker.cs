// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Server.ProgressTracking {
  /// <summary>
  /// Defines the behavior of low oeverhead progress tracker instances.
  /// 
  /// Example:
  /// <code>
  /// using (var progress = (...concrete class creation...) {
  ///   if (progress.Step())
  ///     progress.DisplayProgress((i, n) => Console.WriteLine("Step {0} out of {1}", i, n));
  /// }
  /// </code>
  /// </summary>
  public interface IProgressTracker : IDisposable {
    /// <summary>
    /// Must be called for each step of the progress tracker, returns true if
    /// calling <see cref="DisplayProgress"/> is advised.
    /// </summary>
    bool Step();
    /// <summary>
    /// Called when <see cref="Step"/> returned true. Invokes <paramref
    /// name="displayProgressCallback"/> with the current step count
    /// and total count.
    /// </summary>
    void DisplayProgress(DisplayProgressCallback displayProgressCallback);
  }
}
