// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Linq;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemTree;

namespace VsChromium.Server.Search {
  public class FileSystemTreeVisitor {
    private readonly FileSystemTreeInternal _tree;

    public FileSystemTreeVisitor(FileSystemTreeInternal tree) {
      _tree = tree;
    }

    public Action<DirectoryEntryInternal> VisitDirectory { get; set; }
    public Action<FileName> VisitFile { get; set; }

    public void Visit() {
      _tree.ProjectRoots.ForAll(x => VisitWorker(x));
    }

    private void VisitWorker(DirectoryEntryInternal entry) {
      var stack = new Stack<DirectoryEntryInternal>();
      stack.Push(entry);
      while (stack.Count > 0) {
        var head = stack.Pop();
        VisitDirectory(head);
        head.Files.ForAll(x => VisitFile(x));
        head.DirectoryEntries.ForAll(x => stack.Push(x));
      }
    }
  }
}
