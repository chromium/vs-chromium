// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Server.FileSystemSnapshot;

namespace VsChromium.Server.FileSystem {
  public class FileSystemValidationResult {
    public FileSystemValidationResult() {
      ModifiedFiles = new List<ProjectFileName>();
    }

    public bool NoChanges { get; set; }

    public bool FileModificationsOnly { get; set; }
    public IList<ProjectFileName> ModifiedFiles { get; set; }

    public bool VariousFileChanges { get; set; }
    public FullPathChanges FileChanges { get; set; }

    public bool UnknownChanges { get; set; }
  }
}
