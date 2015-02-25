// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Server.FileSystem {
  public class FileSystemValidationResult {
    public FileSystemValidationResult() {
      ChangedFiles = new List<ProjectFileName>();
      AddedFiles = new List<ProjectFileName>();
      DeletedFiles = new List<ProjectFileName>();
    }

    public bool RecomputeGraph { get; set; }
    public IList<ProjectFileName> ChangedFiles { get; set; }
    public IList<ProjectFileName> AddedFiles { get; set; }
    public IList<ProjectFileName> DeletedFiles { get; set; }
  }
}
