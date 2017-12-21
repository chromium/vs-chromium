// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Files {
  public struct PathChangeEntry {
    private readonly FullPath _basePath;
    private readonly RelativePath _entryPath;
    private readonly PathChangeKind _kind;

    public PathChangeEntry(FullPath basePath, RelativePath entryPath, PathChangeKind kind) {
      _basePath = basePath;
      _entryPath = entryPath;
      _kind = kind;
    }

    public FullPath BasePath { get { return _basePath; } }
    public RelativePath RelativePath { get { return _entryPath; } }
    public FullPath Path { get { return _basePath.Combine(_entryPath); } }
    public PathChangeKind Kind { get { return _kind; } }

    public override string ToString() {
      return string.Format("Change: Path=\"{0}\", Kind={1}", Path.Value, Kind);
    }
  }
}