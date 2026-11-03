// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using System.Threading;

namespace VsChromium.Threads {
  [Export(typeof(ISynchronizationContextProvider))]
  public class SynchronizationContextProvider : ISynchronizationContextProvider {
    private readonly ISynchronizationContext _context;
    private readonly int _threadId;

    [ImportingConstructor]
    public SynchronizationContextProvider(IDispatchThread dispatchThread) {
      _context = new SynchronizationContextDelegate(SynchronizationContext.Current);
      _threadId = dispatchThread.ManagedThreadId;
    }

    public ISynchronizationContext DispatchThreadContext => _context;
  }
}