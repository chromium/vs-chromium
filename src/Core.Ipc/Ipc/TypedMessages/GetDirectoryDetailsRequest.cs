// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc.TypedMessages {
  public class GetDirectoryDetailsRequest : TypedRequest {
    public string Path { get; set; }

    public int MaxFilesByExtensionDetailsCount { get; set; }

    public int MaxLargeFilesDetailsCount { get; set; }
  }
}
