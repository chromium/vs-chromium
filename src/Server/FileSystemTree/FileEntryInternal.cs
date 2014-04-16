// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemTree {
  public class FileEntryInternal : FileSystemEntryInternal {
    private readonly FileName _fileName;

    public FileEntryInternal(FileName fileName) {
      _fileName = fileName;
    }

    public FileName FileName { get { return _fileName; } }
    public override FileSystemName FileSystemName { get { return _fileName; } }
  }
}