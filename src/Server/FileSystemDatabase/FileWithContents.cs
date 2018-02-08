// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemDatabase {
  /// <summary>
  /// Holds a <see cref="VsChromium.Server.FileSystemNames.FileName"/> and its optional
  /// corresponding <see cref="FileContents"/>
  /// </summary>
  public struct FileWithContents {
    private readonly FileName _fileName;
    private readonly FileContents _contents;

    public FileWithContents(FileName fileName, FileContents contents) {
      _fileName = fileName;
      _contents = contents;
    }

    /// <summary>
    /// The file name. Note the file may not exist on disk anymore, or the file
    /// maybe not be indexed. Use FileContent to look for the snapshot of the
    /// file contents at index creation.
    /// </summary>
    public FileName FileName => _fileName;

    /// <summary>
    /// The file contents. May be null if this file is no part of the search
    /// engine text index.
    /// </summary>
    public FileContents Contents => _contents;


    public override string ToString() {
      return $"{_fileName} - {_contents?.ByteLength ?? -1:n0} bytes";
    }
  }

  public static class FileWithContentsExtensions {
    public static bool HasContents(this FileWithContents x) {
      return x.Contents != null && x.Contents.ByteLength > 0;
    }
  }
}
