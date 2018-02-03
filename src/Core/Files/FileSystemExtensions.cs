// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;

namespace VsChromium.Core.Files {
  public static class FileSystemExtensions {
    public static bool FileExists(this IFileSystem fileSystem, FullPath path) {
      var fileInfo = fileSystem.GetFileInfoSnapshot(path);
      return fileInfo.Exists && fileInfo.IsFile;
    }

    public static bool DirectoryExists(this IFileSystem fileSystem, FullPath path) {
      var fileInfo = fileSystem.GetFileInfoSnapshot(path);
      return fileInfo.Exists && fileInfo.IsDirectory;
    }

    public static DateTime GetFileLastWriteTimeUtc(this IFileSystem fileSystem, FullPath path) {
      var fileInfo = fileSystem.GetFileInfoSnapshot(path);
      if (!fileInfo.Exists) {
        throw new IOException(string.Format("File \"{0}\" does not exist", path.Value));
      }
      if (!fileInfo.IsFile) {
        throw new IOException(string.Format("File system entry \"{0}\" is not a file", path.Value));
      }
      return fileInfo.LastWriteTimeUtc;
    }

    /// <summary>
    /// Reads the full contents of a text file into a list of strings.
    /// </summary>
    public static IList<string> ReadAllLines(this IFileSystem fileSystem, FullPath path) {
      var result = new List<string>();
      using (var reader = new StringReader(fileSystem.ReadText(path))) {
        for (var line = reader.ReadLine(); line != null; line = reader.ReadLine()) {
          result.Add(line);
        }
      }
      return result;
    }

  }
}