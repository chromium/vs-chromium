// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemDatabase {
  interface IFileContentsMemoization {
    /// <summary>
    /// Returns the unique instance of <see cref="FileContents"/> identical
    /// to the passed in <paramref name="fileContents"/>.
    /// </summary>
    FileContents Get(FileName fileName, FileContents fileContents);
  }
}