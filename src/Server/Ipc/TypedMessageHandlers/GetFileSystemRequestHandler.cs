// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemTree;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class GetFileSystemRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemProcessor _processor;

    [ImportingConstructor]
    public GetFileSystemRequestHandler(IFileSystemProcessor processor) {
      _processor = processor;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      return new GetFileSystemResponse {
        Tree = _processor.GetTree().ToIpcFileSystemTree()
      };
    }
  }

  public static class VersionedFileSystemTreeExtensions {
    public static Core.Ipc.TypedMessages.FileSystemTree ToIpcFileSystemTree(this VersionedFileSystemTreeInternal tree) {
      return new Core.Ipc.TypedMessages.FileSystemTree {
        Version = tree.Version,
        Root = BuildDirectoryEntry(tree.FileSystemTree.Root)
      };
    }

    private static FileSystemEntry BuildEntry(FileSystemEntryInternal entry) {
      var fileEntry = (entry as FileEntryInternal);
      if (fileEntry != null) {
        return BuildFileEntry(fileEntry);
      }
      var directoryEntry = (entry as DirectoryEntryInternal);
      if (directoryEntry != null) {
        return BuildDirectoryEntry(directoryEntry);
      }
      throw new InvalidOperationException(string.Format("Unknown entry type ({0})", entry.GetType().FullName));
    }

    private static List<FileSystemEntry> BuildEntries(IEnumerable<FileSystemEntryInternal> entries) {
      return entries.Select(x => BuildEntry(x)).ToList();
    }

    private static DirectoryEntry BuildDirectoryEntry(DirectoryEntryInternal directoryEntry) {
      return new DirectoryEntry {
        Name = directoryEntry.IsRoot ? null : directoryEntry.DirectoryName.Name,
        Data = null,
        Entries = BuildEntries(directoryEntry.Entries)
      };
    }

    private static FileSystemEntry BuildFileEntry(FileEntryInternal fileEntry) {
      return new FileEntry {
        Name = fileEntry.FileName.Name,
        Data = null
      };
    }
  }
}
