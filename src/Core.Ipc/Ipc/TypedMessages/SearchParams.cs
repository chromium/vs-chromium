// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class SearchParams {
    public SearchParams() {
      MaxResults = int.MaxValue;
    }

    [ProtoMember(1)]
    public string SearchString { get; set; }

    [ProtoMember(2)]
    public string FilePathPattern { get; set; }

    [ProtoMember(3)]
    public int MaxResults { get; set; }

    [ProtoMember(4)]
    public bool MatchCase { get; set; }

    [ProtoMember(5)]
    public bool MatchWholeWord { get; set; }

    [ProtoMember(6)]
    public bool IncludeSymLinks { get; set; }

    [ProtoMember(7)]
    public bool Regex { get; set; }

    [ProtoMember(8)]
    public bool UseRe2Engine { get; set; }
  }
}
