// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class DirectoryDetails {
    public DirectoryDetails() {
      SearchableFilesByExtensionDetails = new List<FileByExtensionDetails>();
      LargeSearchableFilesDetails = new List<LargeFileDetails>();
      LargeBinaryFilesDetails = new List<LargeFileDetails>();
    }

    [ProtoMember(1)]
    public string Path { get; set; }

    [ProtoMember(2)]
    public long DirectoryCount { get; set; }

    [ProtoMember(3)]
    public long FileCount { get; set; }

    [ProtoMember(4)]
    public long SearchableFileCount { get; set; }

    [ProtoMember(5)]
    public long SearchableFileByteLength { get; set; }

    [ProtoMember(6)]
    public List<FileByExtensionDetails> SearchableFilesByExtensionDetails { get; set; }

    [ProtoMember(7)]
    public List<LargeFileDetails> LargeSearchableFilesDetails { get; set; }

    [ProtoMember(8)]
    public List<LargeFileDetails> LargeBinaryFilesDetails { get; set; }
  }
}