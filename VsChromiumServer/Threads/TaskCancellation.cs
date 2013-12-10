// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Threading;
using VsChromiumCore.Linq;

namespace VsChromiumServer.Threads {
  /// <summary>
  /// Keeps track of cancellation tokensof multiple independent tasks. Tread safe.
  /// </summary>
  public class TaskCancellation {
    private readonly object _lock = new object();
    private volatile List<CancellationTokenSource> _sources = new List<CancellationTokenSource>();

    public void CancelAll() {
      List<CancellationTokenSource> tempCopy = null;

      lock (_lock) {
        if (_sources.Count > 0) {
          tempCopy = _sources;
          _sources = new List<CancellationTokenSource>();
        }
      }

      if (tempCopy != null) {
        tempCopy.ForAll(x => x.Cancel());
        tempCopy.Clear();
      }
    }

    public CancellationToken GetNewToken() {
      var source = new CancellationTokenSource();
      lock (_lock) {
        _sources.Add(source);
      }
      return source.Token;
    }
  }
}
