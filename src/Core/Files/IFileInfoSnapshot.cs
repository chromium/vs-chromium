// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Files {
  /// <summary>
  /// Abstraction over a file or directory contained in the file system.
  /// Exceptions are thrown for attributes that require the file to exist and be
  /// accessible on disk.
  /// </summary>
  public interface IFileInfoSnapshot {
    /// <summary>
    /// The path corresponding to this entry.
    /// </summary>
    FullPath Path { get; }

    /// <summary>
    /// Returns "true" if the entry exists on disk.
    /// Returns "false" if the entry does not exist or cannot be accessed.
    /// </summary>
    bool Exists { get; }

    /// <summary>
    /// Returns "true" if the entry is a file.
    /// Returns "false" if the entry does not exist or cannot be accessed.
    /// </summary>
    bool IsFile { get; }

    /// <summary>
    /// Returns "true" if the entry is a directory.
    /// Returns "false" if the entry does not exist or cannot be accessed.
    /// </summary>
    bool IsDirectory { get; }

    /// <summary>
    /// Returns the UTC <see cref="DateTime"/> of the last access to the entry.
    /// Throws an exception if the file does not exist or cannot be accessed.
    /// </summary>
    DateTime LastWriteTimeUtc { get; }

    /// <summary>
    /// Returns # of bytes the entry uses on disk. Throws an exception if the
    /// file does not exist or cannot be accessed.
    /// </summary>
    long Length { get; }
  }
}