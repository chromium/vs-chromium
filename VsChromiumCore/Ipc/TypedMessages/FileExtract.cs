// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromiumCore.Ipc.TypedMessages {
  [ProtoContract]
  public class FileExtract {
    /// <summary>
    /// The extracted text
    /// </summary>
    [ProtoMember(1)]
    public string Text { get; set; }

    /// <summary>
    /// The character offset of the extracted text.
    /// </summary>
    [ProtoMember(2)]
    public int Offset { get; set; }

    /// <summary>
    /// The number of characters in extracted text.
    /// </summary>
    [ProtoMember(3)]
    public int Length { get; set; }

    /// <summary>
    /// The line number of the extracted text.
    /// </summary>
    [ProtoMember(4)]
    public int LineNumber { get; set; }

    [ProtoMember(5)]
    public int ColumnNumber { get; set; }
  }
}
