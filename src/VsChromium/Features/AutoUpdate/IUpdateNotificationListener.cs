// Copyright 2020 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Features.AutoUpdate {
  /// <summary>
  /// Components listening to "there is a new VsChromium package version
  /// available" event.
  /// </summary>
  public interface IUpdateNotificationListener {
    /// <summary>
    /// Method invoked when there is a new update available. The method is called
    /// on a background thread.
    /// </summary>
    void NotifyUpdate(UpdateInfo updateInfo);
  }
}