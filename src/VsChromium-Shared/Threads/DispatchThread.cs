// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;

namespace VsChromium.Threads {
  [Export(typeof(IDispatchThread))]
  public class DispatchThread : IDispatchThread {
    [ImportingConstructor]
    public DispatchThread() {
      ManagedThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
    }

    public int ManagedThreadId { get; private set; }
  }
}