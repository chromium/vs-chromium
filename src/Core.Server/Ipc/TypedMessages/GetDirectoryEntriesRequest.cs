// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc.TypedMessages {
  public class GetDirectoryEntriesRequest : TypedRequest {
    public string ProjectPath { get; set; }

    public string DirectoryRelativePath { get; set; }

    public override string ToString() {
      return $"{base.ToString()} : Project=\"{ProjectPath}\" RelativePath=\"{DirectoryRelativePath}\"";
    }
  }
}
