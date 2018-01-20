// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Files {
  public partial class DirectoryChangeWatcher {
    private class SharedState {
      private readonly DirectoryChangeWatcher _parentWatcher;

      public SharedState(DirectoryChangeWatcher parentWatcher) {
        _parentWatcher = parentWatcher;
      }

      public DirectoryChangeWatcher ParentWatcher {
        get { return _parentWatcher; }
      }
    }
  }
}