// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.FileNames {
  public interface IFileSystem {
    bool FileExists(FullPath path);
    bool DirectoryExists(FullPath path);

    DateTime GetFileLastWriteTimeUtc(FullPath path);
    string[] ReadAllLines(FullPath path);
  }
}
