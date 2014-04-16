// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemTree {
  public abstract class FileSystemEntryInternal {
    public abstract FileSystemName FileSystemName { get; }
  }
}