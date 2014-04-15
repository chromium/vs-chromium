using System.Collections.ObjectModel;
using VsChromium.Core.FileNames;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemTree {
  public class FileSystemTreeInternal {
    private readonly DirectoryEntryInternal _root;

    public FileSystemTreeInternal(DirectoryEntryInternal root) {
      _root = root;
    }

    public DirectoryEntryInternal Root { get { return _root; } }

    public static FileSystemTreeInternal Empty(IFileSystemNameFactory fileSystemNameFactory) {
      return new FileSystemTreeInternal(new DirectoryEntryInternal(fileSystemNameFactory.Root, new ReadOnlyCollection<FileSystemEntryInternal>(new FileSystemEntryInternal[0])));
    }
  }
}