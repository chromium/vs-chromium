// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Server.Operations;

namespace VsChromium.Server.Search {
  public class FilesLoadedResult {
    public OperationInfo OperationInfo { get; set; }
    /// <summary>
    /// Error if the file loading operation did not complete successfully.
    /// <code>null</code> if successful completion.
    /// </summary>
    public Exception Error { get; set; }
    /// <summary>
    /// The version of the file system tree for which files have been loaded.
    /// </summary>
    public long TreeVersion { get; set; }
  }
}