// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;

namespace VsChromium.Server.FileSystemNames {
  [Export(typeof(IFileSystemNameFactory))]
  public class FileSystemNameFactory : IFileSystemNameFactory {
    private const int BucketCount = 31; // Prime number
    private readonly ConcurrentHashSet<Entry>[] _dictionaries;

    public FileSystemNameFactory() {
      _dictionaries = new ConcurrentHashSet<Entry>[BucketCount];
      for (var i = 0; i < _dictionaries.Length; i++) {
        _dictionaries[i] = new ConcurrentHashSet<Entry>(1024);
      }
    }

    public DirectoryName CreateAbsoluteDirectoryName(FullPath path) {
      return new AbsoluteDirectoryName(path);
    }

    public FileName CreateFileName(DirectoryName parent, string name) {
      return new FileName(parent, InterName(name));
    }

    public DirectoryName CreateDirectoryName(DirectoryName parent, string name) {
      return new RelativeDirectoryName(parent, InterName(name));
    }

    private string InterName(string name) {
      return _dictionaries[name.Length % BucketCount].GetOrAdd(new Entry(name)).Value;
    }

    public void ClearInternedStrings() {
      for (var i = 0; i < _dictionaries.Length; i++) {
        _dictionaries[i].Clear();
      }
    }

    private struct Entry : IEquatable<Entry> {
      private readonly int _hashCode;
      private readonly string _value;

      public Entry(string value) {
        _hashCode = StringComparer.OrdinalIgnoreCase.GetHashCode(value);
        _value = value;
      }

      public string Value {
        get { return _value; }
      }

      public override int GetHashCode() {
        return _hashCode;
      }

      public override bool Equals(object obj) {
        if (obj is Entry) {
          return Equals((Entry)obj);
        }
        return false;
      }

      public bool Equals(Entry other) {
        return _hashCode == other._hashCode &&
               _value == other._value;
      }
    }
  }
}
