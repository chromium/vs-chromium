// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.ObjectModel;
using System.Linq;
using VsChromium.Core.Linq;

namespace VsChromium.Server.FileSystemTree {
  public class FileSystemTreeInternal {
    private readonly ReadOnlyCollection<DirectoryEntryInternal> _projectRoots;

    public FileSystemTreeInternal(ReadOnlyCollection<DirectoryEntryInternal> projectRoots) {
      _projectRoots = projectRoots;
    }

    public ReadOnlyCollection<DirectoryEntryInternal> ProjectRoots { get { return _projectRoots; } }

    public static FileSystemTreeInternal Empty {
      get {
        return new FileSystemTreeInternal(Enumerable.Empty<DirectoryEntryInternal>().ToReadOnlyCollection());
      }
    }
  }
}