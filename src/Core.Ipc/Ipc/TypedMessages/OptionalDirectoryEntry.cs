// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  /// <summary>
  /// A <code>boxed</code> <see cref="DirectoryEntry"/>, used when a <code>null</code>
  /// needs to be serialized/deserialized using protobuf. Since <see cref="DirectoryEntry"/>
  /// is a subclass of <see cref="FileEntry"/>, protobuf cannot correctly deserialize
  /// <code>null</code> values.
  /// </summary>
  [ProtoContract]
  public class OptionalDirectoryEntry {
    public OptionalDirectoryEntry() {
      // To avoid getting "null" value during deserialized using protobuf.
      Value = new DirectoryEntry();
    }

    [ProtoMember(1)]
    public bool HasValue { get; set; }

    [ProtoMember(2)]
    public DirectoryEntry Value { get; set; }
  }
}