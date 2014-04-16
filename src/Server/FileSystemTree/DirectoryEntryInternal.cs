using System.Collections.ObjectModel;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemTree {
  public class DirectoryEntryInternal : FileSystemEntryInternal {
    private readonly DirectoryName _name;
    private readonly ReadOnlyCollection<FileSystemEntryInternal> _children;

    public DirectoryEntryInternal(DirectoryName name, ReadOnlyCollection<FileSystemEntryInternal> children) {
      _name = name;
      _children = children;
    }

    public ReadOnlyCollection<FileSystemEntryInternal> Entries { get { return _children; } }

    public bool IsRoot { get { return FileSystemName.IsRoot; } }
    public DirectoryName Name { get { return _name; } }
    public override FileSystemName FileSystemName { get { return _name; } }
  }
}