// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumServer.FileSystemNames;

namespace VsChromiumServer.Search {
  public class FileSystemTreeVisitor {
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly FileSystemTree _tree;

    public FileSystemTreeVisitor(IFileSystemNameFactory fileSystemNameFactory, FileSystemTree tree) {
      this._fileSystemNameFactory = fileSystemNameFactory;
      this._tree = tree;
    }

    public Action<DirectoryName, DirectoryEntry> VisitDirectory { get; set; }
    public Action<FileName, FileEntry> VisitFile { get; set; }

    public void Visit() {
      VisitWorker(this._fileSystemNameFactory.Root, this._tree.Root);
    }

    public IEnumerable<KeyValuePair<FileName, FileEntry>> Traverse() {
      return VisitWorker2(this._fileSystemNameFactory.Root, this._tree.Root);
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
            var fileName = this._fileSystemNameFactory.CreateFileName(head.DirectoryName, fileEntry.RelativePathName);
            VisitFile(fileName, fileEntry);
          } else {
            var directoryName = child.RelativePathName.RelativeName == ""
                ? this._fileSystemNameFactory.CombineDirectoryNames(head.DirectoryName, child.Name)
                : this._fileSystemNameFactory.CreateDirectoryName(head.DirectoryName, child.RelativePathName);
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
            FileName fileName = this._fileSystemNameFactory.CreateFileName(head.DirectoryName,
                fileEntry.RelativePathName);
            yield return new KeyValuePair<FileName, FileEntry>(fileName, fileEntry);
          } else {
            DirectoryName directoryName = child.RelativePathName.RelativeName == ""
                ? this._fileSystemNameFactory.CombineDirectoryNames(head.DirectoryName, child.Name)
                : this._fileSystemNameFactory.CreateDirectoryName(head.DirectoryName, child.RelativePathName);
            stack.Push(new Entry(directoryName, (DirectoryEntry)child));
          }
        }
      }
    }

    private struct Entry {
      private readonly DirectoryEntry _directoryEntry;
      private readonly DirectoryName _directoryName;

      public Entry(DirectoryName directoryName, DirectoryEntry directoryEntry) {
        this._directoryName = directoryName;
        this._directoryEntry = directoryEntry;
      }

      public DirectoryName DirectoryName {
        get {
          return this._directoryName;
        }
      }

      public DirectoryEntry DirectoryEntry {
        get {
          return this._directoryEntry;
        }
      }
    }
  }
}
