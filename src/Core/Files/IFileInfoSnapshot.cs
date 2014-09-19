// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Files {
  public interface IFileInfoSnapshot {
    bool IsFile { get; }
    bool IsDirectory { get; }

    FullPath Path { get; }
    bool Exists { get; }
    DateTime LastWriteTimeUtc { get; }
  }
}