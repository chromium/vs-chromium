// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;

namespace VsChromium.Core.Files {
  public partial class DirectoryChangeWatcher {
    private class PathChangeRecorder {
      private readonly ConcurrentQueue<ChangeInfo> _lastRecords = new ConcurrentQueue<ChangeInfo>();

      public void RecordChange(ChangeInfo entry) {
        if (_lastRecords.Count >= 100) {
          ChangeInfo temp;
          _lastRecords.TryDequeue(out temp);
        }
        _lastRecords.Enqueue(entry);
      }

      public class ChangeInfo {
        public PathChangeEntry Entry { get; set; }
        public DateTime TimeStampUtc { get; set; }
      }
    }
  }
}