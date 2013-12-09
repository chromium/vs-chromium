// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromiumCore.FileNames;

namespace VsChromiumServer.FileSystemNames {
  public struct FileSystemNameKey : IEquatable<FileSystemNameKey> {
    private readonly string _name;
    private readonly DirectoryName _parentName;

    public FileSystemNameKey(DirectoryName parentName, string name) {
      this._parentName = parentName;
      this._name = name;
    }

    public DirectoryName ParentName {
      get {
        return this._parentName;
      }
    }

    public string Name {
      get {
        return this._name;
      }
    }

    public bool Equals(FileSystemNameKey other) {
      return
          FileSystemNameComparer.Instance.Equals(this._parentName, other._parentName) &&
              SystemPathComparer.Instance.Comparer.Equals(this._name, other._name);
    }
  }
}
