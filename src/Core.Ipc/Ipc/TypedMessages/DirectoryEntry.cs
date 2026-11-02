// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;
using System.Collections.Generic;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class DirectoryEntry : FileSystemEntry {
    public DirectoryEntry() {
      // To avoid getting "null" property if empty collection deserialized using protobuf.
      Entries = new List<FileSystemEntry>();
    }

    [ProtoMember(1)]
    public List<FileSystemEntry> Entries { get; set; }

    public bool IsEmpty => string.IsNullOrEmpty(Name);

    public override string ToString() {
      return $"dir: \"{Name}\", {Entries.Count} children";
    }
  }
}
