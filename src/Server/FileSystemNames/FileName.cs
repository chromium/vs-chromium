// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystemNames {
  public class FileName : FileSystemName, IEquatable<FileName> {
    private readonly DirectoryName _parent;
    private readonly RelativePathName _relativePathName;

    public FileName(DirectoryName parent, RelativePathName relativePathName) {
      if (parent == null)
        throw new ArgumentNullException("parent");
      if (relativePathName.IsEmpty)
        throw new ArgumentException("Relative path name should not be empty", "relativePathName");
      _parent = parent;
      _relativePathName = relativePathName;
    }

    public bool Equals(FileName other) {
      return base.Equals(other);
    }

    public override DirectoryName Parent { get { return _parent; } }

    public override RelativePathName RelativePathName { get { return _relativePathName; } }

    public override bool IsAbsoluteName { get { return false; } }

    public override string Name { get { return _relativePathName.Name; } }

    public override bool IsRoot { get { return false; } }

    public override FullPathName FullPathName {
      get {
        for (var parent = Parent; parent != null; parent = parent.Parent) {
          if (parent.IsAbsoluteName)
            return new FullPathName(PathHelpers.PathCombine(parent.Name, _relativePathName.RelativeName));
        }
        throw new InvalidOperationException("FileName entry does not have a parent with an absolute path.");
      }
    }
  }
}
