// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class FilePositionsData : FileSystemEntryData {
    public FilePositionsData() {
      Positions = new List<FilePositionSpan>();
    }

    [ProtoMember(1)]
    public List<FilePositionSpan> Positions { get; set; }

    /// <summary>
    /// Note: This proprty should not really be present. We used it only to 
    /// count the number of items in a search result.
    /// </summary>
    public override int Count { get { return Positions.Count; } }
  }
}
