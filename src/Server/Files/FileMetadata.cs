// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Files {
  /// <summary>
  /// The last write time and the size of a file, as returned by the enumeration of its directory
  /// (no file opened): used to know if the contents of a file read earlier are still valid (same
  /// rule as the index of git).
  /// </summary>
  public readonly struct FileMetadata : IEquatable<FileMetadata> {
    public FileMetadata(DateTime lastWriteTimeUtc, long length) {
      LastWriteTimeUtc = lastWriteTimeUtc;
      Length = length;
    }

    public DateTime LastWriteTimeUtc { get; }
    public long Length { get; }

    public bool Equals(FileMetadata other) {
      return LastWriteTimeUtc == other.LastWriteTimeUtc && Length == other.Length;
    }

    public override bool Equals(object obj) {
      return obj is FileMetadata other && Equals(other);
    }

    public override int GetHashCode() {
      return HashCode.Combine(LastWriteTimeUtc, Length);
    }

    public static bool operator ==(FileMetadata left, FileMetadata right) => left.Equals(right);
    public static bool operator !=(FileMetadata left, FileMetadata right) => !left.Equals(right);

    public override string ToString() {
      return $"{LastWriteTimeUtc:O}, {Length:n0} bytes";
    }
  }
}
