// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Files {
  /// <summary>
  /// Information associated to a file or directory change notification,
  /// given its <see cref="FullPath"/>.
  /// </summary>
  public struct PathChangeEntry {
    private readonly FullPath _basePath;
    private readonly RelativePath _entryPath;
    private readonly PathChangeKind _changeKind;
    private readonly PathKind _pathKind;

    public PathChangeEntry(FullPath basePath, RelativePath entryPath, PathChangeKind changeKind, PathKind pathKind) {
      _basePath = basePath;
      _entryPath = entryPath;
      _changeKind = changeKind;
      _pathKind = pathKind;
    }

    public FullPath BasePath { get { return _basePath; } }
    public RelativePath RelativePath { get { return _entryPath; } }
    public FullPath Path { get { return _basePath.Combine(_entryPath); } }
    public PathChangeKind ChangeKind { get { return _changeKind; } }
    public PathKind PathKind { get { return _pathKind; }
    }

    public override string ToString() {
      return string.Format("Change: Path=\"{0}\", PathKind={1}, ChangeKind={2}", Path.Value, PathKind, ChangeKind);
    }
  }

  public enum PathKind {
    /// <summary>The path is definitely a file path</summary>
    File,
    /// <summary>The path is definitely a directory path</summary>
    Directory,
    /// <summary>The path is *either* a file or a directory, i.e. we don't know which one, but it is not both.</summary>
    FileOrDirectory,
    /// <summary>
    /// The path is *both* a directory and a file path, probably because it was a file path at some point
    /// in time, then became a directory path later on, or vice versa.
    /// </summary>
    FileAndDirectory,
  }
}