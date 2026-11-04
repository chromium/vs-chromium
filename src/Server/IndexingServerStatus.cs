// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Server {
  public enum IndexingServerStatus {
    /// <summary>
    /// The server is actually running
    /// </summary>
    Idle,
    /// <summary>
    /// The server received a request to go in a "pause" state
    /// </summary>
    Paused,
    /// <summary>
    /// The server put itself in "pause" mode because of a file system watcher buffer overflow error
    /// </summary>
    Yield,
    /// <summary>
    /// The server is busy indexing the file system
    /// </summary>
    Busy,
    /// <summary>
    /// The index of the last session (index cache) is in use, and is being verified in the
    /// background (scan and incremental update): searches may contain deleted files or miss new
    /// files until it ends.
    /// </summary>
    Verifying,
  }
}
