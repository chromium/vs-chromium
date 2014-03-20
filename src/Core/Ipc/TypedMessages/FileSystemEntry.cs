// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;
using VsChromium.Core.FileNames;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  [ProtoInclude(10, typeof(FileEntry))]
  [ProtoInclude(11, typeof(DirectoryEntry))]
  public abstract class FileSystemEntry {
    [ProtoMember(1)]
    public string Name { get; set; }

    [ProtoMember(2)]
    public FileSystemEntryData Data { get; set; }

    /// <summary>
    /// Note: Only set in VsChromiumServer.
    /// </summary>
    public RelativePathName RelativePathName { get; set; }

    public abstract override string ToString();
  }
}
