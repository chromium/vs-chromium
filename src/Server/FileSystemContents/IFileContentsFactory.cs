// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Files;

namespace VsChromium.Server.FileSystemContents {
  public interface IFileContentsFactory {
    FileContents ReadFileContents(FullPath path);

    /// <summary>
    /// Reads a file of the project whose root is <paramref name="projectRoot"/>: the contents are
    /// allocated in the arenas of the project (see <see cref="VsChromium.Core.Memory.ProjectArenaAllocators"/>).
    /// </summary>
    FileContents ReadFileContents(FullPath path, FullPath projectRoot);
  }
}
