// Copyright 2020 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Server.ProgressTracking {
  public interface ITickCountProvider {
    /// <returns>
    /// Returns a integer value containing the amount of time in milliseconds that has passed since some arbitrary initial moment.
    /// </returns>
    long TickCount { get; }
  }
}