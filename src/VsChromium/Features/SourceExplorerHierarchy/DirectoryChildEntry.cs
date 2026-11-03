// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Protocol.Generated;

namespace VsChromium.Features.SourceExplorerHierarchy {
  /// <summary>
  /// An entry (sub directory or file) of a directory returned by the server
  /// (see <see cref="DirectoryEntries"/>).
  /// </summary>
  public struct DirectoryChildEntry {
    public DirectoryChildEntry(string name, bool isDirectory) {
      Name = name;
      IsDirectory = isDirectory;
    }

    public string Name { get; }
    public bool IsDirectory { get; }
  }

  public static class DirectoryEntriesExtensions {
    /// <summary>
    /// The entries of a directory, in the order displayed in the Source
    /// Explorer: the sub directories, then the files (each in the order of the
    /// server).
    /// </summary>
    public static IList<DirectoryChildEntry> GetChildEntries(this DirectoryEntries entries) {
      var result = new List<DirectoryChildEntry>();
      if (entries.DirectoryNames != null) {
        foreach (var name in entries.DirectoryNames) {
          result.Add(new DirectoryChildEntry(name, true));
        }
      }
      if (entries.FileNames != null) {
        foreach (var name in entries.FileNames) {
          result.Add(new DirectoryChildEntry(name, false));
        }
      }
      return result;
    }
  }
}
