// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc.TypedMessages {
  /// <summary>
  /// A <code>boxed</code> <see cref="DirectoryEntry"/>, used when a <code>null</code>
  /// needs to be serialized/deserialized (historical: protobuf, the former wire format, could not
  /// correctly deserialize <code>null</code> values of a sub-class).
  /// </summary>
  public class OptionalDirectoryEntry {
    public OptionalDirectoryEntry() {
      // To avoid getting a "null" value during deserialization.
      Value = new DirectoryEntry();
    }

    public bool HasValue { get; set; }

    public DirectoryEntry Value { get; set; }
  }
}
