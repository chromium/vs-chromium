// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem {
  public class FileSystemValidationResult {
    public FileSystemValidationResult() {
      ChangedFiles = new List<Tuple<IProject, FileName>>();
    }

    public bool RecomputeGraph { get; set; }
    public IList<Tuple<IProject, FileName>> ChangedFiles { get; set; }
  }
}
