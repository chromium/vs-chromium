using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemTree;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  public static class VersionedFileSystemTreeExtensions {
    public static Core.Ipc.TypedMessages.FileSystemTree ToIpcFileSystemTree(this VersionedFileSystemTreeInternal tree) {
      return new Core.Ipc.TypedMessages.FileSystemTree {
        Version = tree.Version,
        Root = BuildFileSystemTree(tree.FileSystemTree)
      };
    }

    private static DirectoryEntry BuildFileSystemTree(FileSystemTreeInternal fileSystemTree) {
      return new DirectoryEntry {
        Name = null,
        Data = null,
        Entries = fileSystemTree.ProjectRoots.Select(x => BuildDirectoryEntry(x)).Cast<FileSystemEntry>().ToList()
      };
    }

    private static DirectoryEntry BuildDirectoryEntry(DirectoryEntryInternal directoryEntry) {
      return new DirectoryEntry {
        Name = directoryEntry.DirectoryName.Name,
        Data = null,
        Entries = BuildEntries(directoryEntry)
      };
    }

    private static FileSystemEntry BuildFileEntry(FileName filename) {
      return new FileEntry {
        Name = filename.Name,
        Data = null
      };
    }
    private static List<FileSystemEntry> BuildEntries(DirectoryEntryInternal directoryEntry) {
      return directoryEntry.DirectoryEntries
        .Select(x => BuildDirectoryEntry(x))
        .Concat(directoryEntry.Files.Select(x => BuildFileEntry(x)))
        .ToList();
    }

  }
}