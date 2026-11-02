// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class GetDirectoryEntriesMultipleRequest : TypedRequest {
    public GetDirectoryEntriesMultipleRequest() {
      RelativePathList = new List<string>();
    }

    [ProtoMember(1)]
    public string ProjectPath { get; set; }

    [ProtoMember(2)]
    public List<string> RelativePathList { get; set; }

    public override string ToString() {
      return $"{base.ToString()} : Project=\"{ProjectPath}\", # of Paths={RelativePathList.Count}";
    }
  }
}