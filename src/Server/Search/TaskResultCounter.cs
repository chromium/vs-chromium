// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Server.Search {
  /// <summary>
  /// Note: Instances of this class are technically not thread safe, but it is
  /// nonetheless ok to call them from multiple thread concurrently, as the
  /// worst that can happen is that parallel tasks will run a bit too much. This
  /// is assuming of course this class is only used to bound the number of
  /// results returned by tasks run in parallel.
  /// </summary>
  public class TaskResultCounter {
    private readonly int _maxResults;
    private int _count;

    public TaskResultCounter(int maxResults) {
      _maxResults = maxResults;
    }

    public bool Done { get { return _count >= _maxResults; } }
    public int Count { get { return _count; } }

    public void Add(int count) {
      _count += count;
    }

    public void Increment() {
      Add(1);
    }
  }
}
