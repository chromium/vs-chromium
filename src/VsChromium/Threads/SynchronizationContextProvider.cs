// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using System.Threading;

namespace VsChromium.Threads {
  [Export(typeof(ISynchronizationContextProvider))]
  public class SynchronizationContextProvider : ISynchronizationContextProvider {
    private readonly ISynchronizationContext _context;

    public SynchronizationContextProvider() {
      _context = new SynchronizationContextDelegate(SynchronizationContext.Current);
    }
    public ISynchronizationContext UIContext { get { return _context; } }
  }
}