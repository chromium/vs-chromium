// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class GetDatabaseDetailsResponse : TypedResponse {
    public GetDatabaseDetailsResponse() {
      Projects = new List<ProjectDetails>();
    }

    [ProtoMember(1)]
    public List<ProjectDetails> Projects { get; set; }
  }

  [ProtoContract]
  public class ProjectDetails {
    public ProjectDetails() {
      FilesByExtensionDetails = new List<FileByExtensionDetails>();
    }

    [ProtoMember(1)]
    public string RootPath { get; set; }

    [ProtoMember(2)]
    public long DirectoryCount { get; set; }

    [ProtoMember(3)]
    public long FileCount { get; set; }

    [ProtoMember(4)]
    public long SearchableFileCount { get; set; }

    [ProtoMember(5)]
    public long SearchableFileByteLength { get; set; }

    [ProtoMember(6)]
    public List<FileByExtensionDetails> FilesByExtensionDetails { get; set; }
  }

  [ProtoContract]
  public class FileByExtensionDetails {
    [ProtoMember(1)]
    public string FileExtension { get; set; }

    [ProtoMember(2)]
    public long FileCount { get; set; }

    [ProtoMember(3)]
    public long FilesByteLength { get; set; }
  }
}