// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.Threading;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemDatabase {
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

  public class FileContentsMemoization : IFileContentsMemoization {
    private readonly ConcurrentDictionary<MapKey, FileContents> _map = new ConcurrentDictionary<MapKey, FileContents>();

    public FileContents Get(FileName fileName, FileContents fileContents) {
      var key = new MapKey(fileName, fileContents);
      return _map.GetOrAdd(key, fileContents);
    }

    public int Count { get { return _map.Count; } }

    private struct MapKey : IEquatable<MapKey> {
      private readonly FileContents _fileContents;
      private readonly int _hashCode;

      public MapKey(FileName fileName, FileContents fileContents) {
        _fileContents = fileContents;
        _hashCode = HashCode.Combine(
            _fileContents.UtcLastModified.GetHashCode(),
            _fileContents.ByteLength.GetHashCode());
      }

      public bool Equals(MapKey other) {
        return
          _fileContents.UtcLastModified.Equals(other._fileContents.UtcLastModified) &&
          _fileContents.HasSameContents(other._fileContents);
      }

      public override int GetHashCode() {
        return _hashCode;
      }
    }
  }
}