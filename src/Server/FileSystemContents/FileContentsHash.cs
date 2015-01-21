// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Security.Cryptography;

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
      using (var md5 = MD5.Create()) {
        using (var stream = memory.CreateSteam()) {
          byte[] hash = md5.ComputeHash(stream);
          return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
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
