// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class GetDatabaseDetailsResponse : TypedResponse {
    public GetDatabaseDetailsResponse() {
      Projects = new List<ProjectDetails>();
    }

    [ProtoMember(1)]
    public List<ProjectDetails> Projects { get; set; }
  }
}