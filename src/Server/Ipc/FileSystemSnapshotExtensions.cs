using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.Ipc {
  public static class FileSystemSnapshotExtensions {
    public static FileSystemTree_Obsolete ToIpcFileSystemTree(this FileSystemSnapshot tree) {
      return new FileSystemTree_Obsolete {
        Version = tree.Version,
        Root = BuildFileSystemTreeRoot(tree)
      };
    }

    public static FileSystemTree ToIpcCompactFileSystemTree(this FileSystemSnapshot tree) {
      return new FileSystemTree {
        Version = tree.Version,
        Projects = BuildCompactProjectEntries(tree)
      };
    }

    private static List<ProjectEntry> BuildCompactProjectEntries(FileSystemSnapshot tree) {
      return tree.ProjectRoots
        .Select(x => new ProjectEntry {
          RootPath = x.Project.RootPath.Value
        })
        .ToList();
    }

    private static DirectoryEntry BuildFileSystemTreeRoot(FileSystemSnapshot fileSystemSnapshot) {
      return new DirectoryEntry {
        Name = null,
        Data = null,
        Entries = fileSystemSnapshot.ProjectRoots.Select(x => BuildDirectoryEntry(x.Directory)).Cast<FileSystemEntry>().ToList()
      };
    }

    private static DirectoryEntry BuildDirectoryEntry(DirectorySnapshot directoryEntry) {
      return new DirectoryEntry {
        Name = (directoryEntry.DirectoryName.IsAbsoluteName ? directoryEntry.DirectoryName.FullPath.Value : directoryEntry.DirectoryName.Name),
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
    private static List<FileSystemEntry> BuildEntries(DirectorySnapshot directoryEntry) {
      return directoryEntry.ChildDirectories
        .Select(x => BuildDirectoryEntry(x))
        .Concat(directoryEntry.ChildFiles.Select(x => BuildFileEntry(x)))
        .ToList();
    }

  }
}