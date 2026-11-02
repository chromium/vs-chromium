// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc.TypedMessages {
  public class LargeFileDetails {
    public string RelativePath { get; set; }

    public long ByteLength { get; set; }
  }
}
