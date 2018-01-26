// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.ObjectModel;

namespace VsChromium.Server.FileSystem {
  public class FilesChangedEventArgs : EventArgs {
    /// <summary>
    /// The file system snapshot active at the time the event was fired.
    /// </summary>
    public FileSystemSnapshot FileSystemSnapshot { get; set; }

    /// <summary>
    /// The list of changed files.
    /// </summary>
    public ReadOnlyCollection<ProjectFileName> ChangedFiles { get; set; }
  }
}
