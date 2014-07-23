// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading;
using VsChromium.Core.Logging;

namespace VsChromium.Threads {
  public class SynchronizationContextDelegate : ISynchronizationContext {
    private readonly SynchronizationContext _current;

    public SynchronizationContextDelegate(SynchronizationContext current) {
      _current = current;
    }

    public void Post(Action action) {
      _current.Post(_ => Logger.WrapActionInvocation(action), null);
    }
  }
}