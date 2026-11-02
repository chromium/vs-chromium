// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc.TypedMessages {
  public class FileSystemTree_Obsolete {
    /// <summary>
    /// The version of this snapshot of the tree.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// The Root entry has a "null" name, and contains one directory entry per
    /// project known to the server. Each project entry contains the hierarchy
    /// of directory and file entries as they appear in the file system under
    /// that project path.
    /// </summary>
    public DirectoryEntry Root { get; set; }
  }
}
