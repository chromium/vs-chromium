// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Threading;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemDatabase.Builder {
  public class NullFileContentsMemoization : IFileContentsMemoization {
    private int _count;

    public FileContents Get(FileName fileName, FileContents fileContents) {
      Interlocked.Increment(ref _count);
      return fileContents;
    }

    public int Count {
      get { return _count; }
    }
  }
}