// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.ObjectModel;

namespace VsChromium.Server.FileSystem {
  public class FilesChangedEventArgs : EventArgs {
    public ReadOnlyCollection<ProjectFileName> ChangedFiles { get; set; }
  }
}