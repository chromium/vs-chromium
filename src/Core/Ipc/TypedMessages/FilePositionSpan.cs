// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public struct FilePositionSpan {
    /// <summary>
    /// The position, in character offset, of the first character of the span.
    /// </summary>
    [ProtoMember(1)]
    public int Position { get; set; }

    /// <summary>
    /// The length, in number of characters, of the span.
    /// </summary>
    [ProtoMember(2)]
    public int Length { get; set; }
  }
}
