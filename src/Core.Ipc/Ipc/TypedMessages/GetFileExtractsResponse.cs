// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Core.Ipc.TypedMessages {
  public class GetFileExtractsResponse : TypedResponse {
    public GetFileExtractsResponse() {
      FileExtracts = new List<FileExtract>();
    }

    public string FileName { get; set; }

    public List<FileExtract> FileExtracts { get; set; }
  }
}
