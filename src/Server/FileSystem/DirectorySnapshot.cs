// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystem {
  /// <summary>
  /// A directory snapshot contains the directory name, the list of
  /// sub-directories and the list of file names directly contained in the
  /// directory.
  /// </summary>
  public class DirectorySnapshot {
    private readonly DirectoryData _directoryData;
    private readonly IList<DirectorySnapshot> _childDirectories;
    private readonly IList<FileName> _childFiles;
    private readonly FileMetadata[] _childFileMetadata;
    private readonly int[] _symLinkFileIndices;

    public DirectorySnapshot(DirectoryData directoryData, IList<DirectorySnapshot> childDirectories, IList<FileName> childFiles,
      FileMetadata[] childFileMetadata) : this(directoryData, childDirectories, childFiles, childFileMetadata, null) {
    }

    public DirectorySnapshot(DirectoryData directoryData, IList<DirectorySnapshot> childDirectories, IList<FileName> childFiles,
      FileMetadata[] childFileMetadata, int[] symLinkFileIndices) {
      if (childFileMetadata.Length != childFiles.Count)
        throw new ArgumentException("There must be one metadata entry per file.", nameof(childFileMetadata));
      _directoryData = directoryData;
      _childDirectories = childDirectories;
      _childFiles = childFiles;
      _childFileMetadata = childFileMetadata;
      _symLinkFileIndices = symLinkFileIndices != null && symLinkFileIndices.Length == 0 ? null : symLinkFileIndices;
    }

    /// <summary>
    /// The directory name
    /// </summary>
    public DirectoryName DirectoryName {
      get { return _directoryData.DirectoryName; }
    }

    public bool IsSymLink {
      get { return _directoryData.IsSymLink; }
    }

    /// <summary>The 128-bit file id of the directory, 0 if unknown (see <see cref="FileSystem.DirectoryData.FileId"/>).</summary>
    public UInt128 FileId {
      get { return _directoryData.FileId; }
    }

    public DirectoryData DirectoryData {
      get { return _directoryData; }
    }

    /// <summary>
    /// The indices (in <see cref="ChildFiles"/>, increasing) of the files that are symbolic links
    /// (reparse points): their metadata are the metadata of their target, whose changes are not
    /// changes of this directory (see the change journal catch-up).
    /// </summary>
    public ReadOnlySpan<int> SymLinkFileIndices {
      get { return _symLinkFileIndices; }
    }

    internal int[] SymLinkFileIndicesArray {
      get { return _symLinkFileIndices; }
    }

    public bool IsSymLinkFile(int index) {
      return _symLinkFileIndices != null && Array.BinarySearch(_symLinkFileIndices, index) >= 0;
    }
    /// <summary>
    /// The list of sub-directories.
    /// </summary>
    public IList<DirectorySnapshot> ChildDirectories {
      get { return _childDirectories; }
    }

    /// <summary>
    /// The list of files contained in this directory.
    /// </summary>
    public IList<FileName> ChildFiles {
      get { return _childFiles; }
    }

    /// <summary>
    /// The last write time and size of each file of <see cref="ChildFiles"/> (same order), as
    /// seen by the directory enumeration that found the file. Note: Not updated when a file is
    /// modified (the directory is not enumerated again): the contents of the file have the time
    /// of their last read.
    /// </summary>
    public ReadOnlySpan<FileMetadata> ChildFileMetadata {
      get { return _childFileMetadata; }
    }

    /// <summary>
    /// The metadata array (shared by the snapshots of an unchanged directory).
    /// </summary>
    internal FileMetadata[] ChildFileMetadataArray {
      get { return _childFileMetadata; }
    }
  }
}
