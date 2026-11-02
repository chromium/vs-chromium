// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Core.Ipc.TypedMessages {
  public class GetDirectoryEntriesMultipleRequest : TypedRequest {
    public GetDirectoryEntriesMultipleRequest() {
      RelativePathList = new List<string>();
    }

    public string ProjectPath { get; set; }

    public List<string> RelativePathList { get; set; }

    public override string ToString() {
      return $"{base.ToString()} : Project=\"{ProjectPath}\", # of Paths={RelativePathList.Count}";
    }
  }
}
