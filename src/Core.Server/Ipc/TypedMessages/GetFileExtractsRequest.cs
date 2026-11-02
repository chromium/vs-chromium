// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Core.Ipc.TypedMessages {
  public class GetFileExtractsRequest : TypedRequest {
    public GetFileExtractsRequest() {
      Positions = new List<FilePositionSpan>();
    }

    public string FileName { get; set; }

    public List<FilePositionSpan> Positions { get; set; }

    public int MaxExtractLength { get; set; }
  }
}
