// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.Search {
  public class FileData {
    private readonly FileName _fileName;
    private FileContents _contents;

    public FileData(FileName fileName, FileContents contents) {
      if (fileName == null)
        throw new ArgumentNullException("fileName");

      _fileName = fileName;
      _contents = contents;
    }

    /// <summary>
    /// The file name. Note the file may not exist on disk anymore, or the file
    /// maybe not be indexed. Use FileContent to look for the snapshot of the
    /// file contents at index creation.
    /// </summary>
    public FileName FileName { get { return _fileName; } }

    /// <summary>
    /// The file contents. May be null if this file is no part of the search
    /// engine text index.
    /// </summary>
    public FileContents Contents { get { return _contents; } }


    public override string ToString() {
      return string.Format("{0} - {1:n0} bytes", _fileName, _contents == null ? -1 : _contents.ByteLength);
    }

    public void UpdateContents(FileContents fileContents) {
      Interlocked.Exchange(ref _contents, fileContents);
    }
  }
}
