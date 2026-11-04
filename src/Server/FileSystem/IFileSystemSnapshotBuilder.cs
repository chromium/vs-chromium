// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Threading;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem {
  public interface IFileSystemSnapshotBuilder {
    FileSystemSnapshot Compute(
      IFileSystemNameFactory fileNameFactory, 
      FileSystemSnapshot oldSnapshot,
      FullPathChanges pathChanges /* may be null */,
      IList<IProject> projects, 
      int version,
      CancellationToken cancellationToken,
      /* The trees of projects not to scan (index cache) */
      IReadOnlyDictionary<FullPath, DirectorySnapshot> precomputedRoots = null,
      /* Projects to scan entirely, even if path changes are given */
      ISet<FullPath> fullScanRoots = null);
  }
}
