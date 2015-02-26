// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Utility;

namespace VsChromium.Server.FileSystemContents {
  public class FileContentsHash : IEquatable<FileContentsHash> {
    private readonly string _md5;
    private readonly int _hashCode;

    public FileContentsHash(FileContentsMemory memory) {
      _md5 = CreateHash(memory);
      _hashCode = StringComparer.Ordinal.GetHashCode(_md5);
    }

    public string Value {
      get { return _md5; }
    }

    private static string CreateHash(FileContentsMemory memory) {
      using (var stream = memory.CreateSteam()) {
        return MD5Hash.CreateHash(stream);
      }
    }

    public bool Equals(FileContentsHash other) {
      if (other == null)
        return false;

      return StringComparer.Ordinal.Equals(this._md5, other._md5);
    }

    public override bool Equals(object obj) {
      return Equals(obj as FileContentsHash);
    }

    public override int GetHashCode() {
      return _hashCode;
    }
  }
}
