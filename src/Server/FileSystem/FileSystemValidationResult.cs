// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Server.FileSystemScanSnapshot;

namespace VsChromium.Server.FileSystem {
  public class FileSystemValidationResult {
    public FileSystemValidationResult() {
      ModifiedFiles = new List<ProjectFileName>();
    }

    public FileSystemValidationResultKind Kind { get; set; }

    public IList<ProjectFileName> ModifiedFiles { get; set; }

    public FullPathChanges FileChanges { get; set; }
  }

  public enum FileSystemValidationResultKind {
    NoChanges,
    FileModificationsOnly,
    VariousFileChanges,
    UnknownChanges
  }
}
