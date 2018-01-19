// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Server {
  public class IndexingServerStateUpdatedEventArgs : EventArgs {
    public IndexingServerState State { get; set; }
  }
}