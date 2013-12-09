// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using System.Threading;

namespace VsChromiumCore.Ipc {
  [Export(typeof(IIpcRequestIdFactory))]
  public class IpcRequestIdFactory : IIpcRequestIdFactory {
    private long _nextId = 1;

    public long GetNextId() {
      return Interlocked.Increment(ref this._nextId);
    }
  }
}
