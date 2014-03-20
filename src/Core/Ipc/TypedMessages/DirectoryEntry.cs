// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class DirectoryEntry : FileSystemEntry {
    public DirectoryEntry() {
      // To avoid getting "null" property if empty collection deserialized using protobuf.
      Entries = new List<FileSystemEntry>();
    }

    [ProtoMember(1)]
    public List<FileSystemEntry> Entries { get; set; }

    public bool IsRoot { get { return Name == null; } }

    public override string ToString() {
      return string.Format("d:{0}, {1} children", Name ?? string.Empty, Entries.Count);
    }
  }
}
