// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Server {
  public class IndexingServerState {
    public IndexingServerStatus Status { get; set; }
    public DateTime LastIndexUpdateUtc { get; set; }
  }
}