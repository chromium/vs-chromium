// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

#if !NETCOREAPP
using System.ComponentModel.Composition;
#endif
using System.Threading;

namespace VsChromium.Core.Ipc {
#if !NETCOREAPP
  [Export(typeof(IIpcRequestIdFactory))]
#endif
  public class IpcRequestIdFactory : IIpcRequestIdFactory {
    private long _nextId = 1;

    public long GetNextId() {
      return Interlocked.Increment(ref _nextId);
    }
  }
}
