// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromiumServer.FileSystemNames;

namespace VsChromiumServer.FileSystem {
  public class FileSystemValidationResult {
    public FileSystemValidationResult() {
      ChangeFiles = new List<FileName>();
    }

    public bool RecomputeGraph { get; set; }
    public IList<FileName> ChangeFiles { get; set; }
  }
}
