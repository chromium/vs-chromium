// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc.TypedMessages {
  public class GetDirectoryEntriesResponse : TypedResponse {
    /// <summary>
    /// <code>null</code> if the requested directory is not found in the
    /// active file system snapshpt.
    /// </summary>
    public DirectoryEntry DirectoryEntry { get; set; }
  }
}
