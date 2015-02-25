// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Server.FileSystemSnapshot;

namespace VsChromium.Server.FileSystem {
  public class FileSystemValidationResult {
    public FileSystemValidationResult() {
      ChangedFiles = new List<ProjectFileName>();
    }

    public bool RecomputeGraph { get; set; }
    public IList<ProjectFileName> ChangedFiles { get; set; }
    public FullPathChanges FullPathChanges { get; set; }
  }
}
