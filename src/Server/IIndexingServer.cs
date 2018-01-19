// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Server {
  public interface IIndexingServer {
    void Pause();
    void Resume();
    void TogglePausedRunning();

    IndexingServerState CurrentState { get; }

    event EventHandler<IndexingServerStateUpdatedEventArgs> StateUpdated;
  }
}