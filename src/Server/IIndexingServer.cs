// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Files;

namespace VsChromium.Server {
  public interface IIndexingServer {
    IndexingServerState CurrentState { get; }

    void Pause();
    void Resume();
    void Refresh();

    /// <summary>
    /// Rebuilds the index of a project ("project/rebuild"): deletes its index cache, rescans it and
    /// reads all its files again. The other projects are unchanged.
    /// </summary>
    void RebuildProject(FullPath projectRoot);

    event EventHandler<IndexingServerStateUpdatedEventArgs> StateUpdated;
  }
}
