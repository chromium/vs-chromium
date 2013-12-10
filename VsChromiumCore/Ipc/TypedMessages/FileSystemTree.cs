// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromiumCore.Ipc.TypedMessages {
  [ProtoContract]
  public class FileSystemTree {
    [ProtoMember(1)]
    public int Version { get; set; }

    [ProtoMember(2)]
    public DirectoryEntry Root { get; set; }

    public static FileSystemTree Empty {
      get {
        return new FileSystemTree {
          Version = 0,
          Root = new DirectoryEntry {
            Name = ""
          }
        };
      }
    }
  }
}
