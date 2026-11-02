// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Core.Ipc.TypedMessages {
  public class DirectoryDetails {
    public DirectoryDetails() {
      SearchableFilesByExtensionDetails = new List<FileByExtensionDetails>();
      LargeSearchableFilesDetails = new List<LargeFileDetails>();
      BinaryFilesByExtensionDetails = new List<FileByExtensionDetails>();
      LargeBinaryFilesDetails = new List<LargeFileDetails>();
    }

    public string Path { get; set; }

    public long DirectoryCount { get; set; }

    public long FileCount { get; set; }

    public long SearchableFilesCount { get; set; }

    public long SearchableFilesByteLength { get; set; }

    public long BinaryFilesCount { get; set; }

    public long BinaryFilesByteLength { get; set; }

    public List<FileByExtensionDetails> SearchableFilesByExtensionDetails { get; set; }

    public List<LargeFileDetails> LargeSearchableFilesDetails { get; set; }

    public List<FileByExtensionDetails> BinaryFilesByExtensionDetails { get; set; }

    public List<LargeFileDetails> LargeBinaryFilesDetails { get; set; }
  }
}
