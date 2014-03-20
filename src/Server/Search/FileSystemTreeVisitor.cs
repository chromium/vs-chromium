// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.Search {
  public class FileSystemTreeVisitor {
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly FileSystemTree _tree;

    public FileSystemTreeVisitor(IFileSystemNameFactory fileSystemNameFactory, FileSystemTree tree) {
      _fileSystemNameFactory = fileSystemNameFactory;
      _tree = tree;
    }

    public Action<DirectoryName, DirectoryEntry> VisitDirectory { get; set; }
    public Action<FileName, FileEntry> VisitFile { get; set; }

    public void Visit() {
      VisitWorker(_fileSystemNameFactory.Root, _tree.Root);
    }

    public IEnumerable<KeyValuePair<FileName, FileEntry>> Traverse() {
      return VisitWorker2(_fileSystemNameFactory.Root, _tree.Root);
    }

    private void VisitWorker(DirectoryName directory, DirectoryEntry entry) {
      var stack = new Stack<Entry>();
      stack.Push(new Entry(directory, entry));
      while (stack.Count > 0) {
        var head = stack.Pop();
        VisitDirectory(head.DirectoryName, head.DirectoryEntry);

        foreach (var child in head.DirectoryEntry.Entries) {
          var fileEntry = child as FileEntry;
          if (fileEntry != null) {
            var fileName = _fileSystemNameFactory.CreateFileName(head.DirectoryName, fileEntry.RelativePathName);
            VisitFile(fileName, fileEntry);
          } else {
            var directoryName = child.RelativePathName.RelativeName == ""
                                  ? _fileSystemNameFactory.CombineDirectoryNames(head.DirectoryName, child.Name)
                                  : _fileSystemNameFactory.CreateDirectoryName(head.DirectoryName, child.RelativePathName);
            stack.Push(new Entry(directoryName, (DirectoryEntry)child));
          }
        }
      }
    }

    private IEnumerable<KeyValuePair<FileName, FileEntry>> VisitWorker2(DirectoryName directory, DirectoryEntry entry) {
      var stack = new Stack<Entry>();
      stack.Push(new Entry(directory, entry));
      while (stack.Count > 0) {
        var head = stack.Pop();

        foreach (var child in head.DirectoryEntry.Entries) {
          var fileEntry = child as FileEntry;
          if (fileEntry != null) {
            FileName fileName = _fileSystemNameFactory.CreateFileName(head.DirectoryName,
                                                                      fileEntry.RelativePathName);
            yield return new KeyValuePair<FileName, FileEntry>(fileName, fileEntry);
          } else {
            DirectoryName directoryName = child.RelativePathName.RelativeName == ""
                                            ? _fileSystemNameFactory.CombineDirectoryNames(head.DirectoryName, child.Name)
                                            : _fileSystemNameFactory.CreateDirectoryName(head.DirectoryName, child.RelativePathName);
            stack.Push(new Entry(directoryName, (DirectoryEntry)child));
          }
        }
      }
    }

    private struct Entry {
      private readonly DirectoryEntry _directoryEntry;
      private readonly DirectoryName _directoryName;

      public Entry(DirectoryName directoryName, DirectoryEntry directoryEntry) {
        _directoryName = directoryName;
        _directoryEntry = directoryEntry;
      }

      public DirectoryName DirectoryName { get { return _directoryName; } }

      public DirectoryEntry DirectoryEntry { get { return _directoryEntry; } }
    }
  }
}
