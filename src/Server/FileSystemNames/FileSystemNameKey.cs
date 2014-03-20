// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystemNames {
  public struct FileSystemNameKey : IEquatable<FileSystemNameKey> {
    private readonly string _name;
    private readonly DirectoryName _parentName;

    public FileSystemNameKey(DirectoryName parentName, string name) {
      _parentName = parentName;
      _name = name;
    }

    public DirectoryName ParentName { get { return _parentName; } }

    public string Name { get { return _name; } }

    public bool Equals(FileSystemNameKey other) {
      return
        FileSystemNameComparer.Instance.Equals(_parentName, other._parentName) &&
        SystemPathComparer.Instance.Comparer.Equals(_name, other._name);
    }
  }
}
