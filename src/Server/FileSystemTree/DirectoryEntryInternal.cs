using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemTree {
  public class DirectoryEntryInternal : FileSystemEntryInternal {
    private readonly DirectoryName _name;
    private readonly List<FileSystemEntryInternal> _children;

    public DirectoryEntryInternal(DirectoryName name, ReadOnlyCollection<FileSystemEntryInternal> children) {
      _name = name;
      // TODO(rpaquay): Make this into a ReadOnlyCollection
      _children = children.ToList();
    }

    // TODO(rpaquay): Make this into a ReadOnlyCollection
    //public ReadOnlyCollection<FileSystemEntryInternal> Entries { get { return _children; } }
    public List<FileSystemEntryInternal> Entries { get { return _children; } }

    public bool IsRoot { get { return FileSystemName.IsRoot; } }
    public DirectoryName Name { get { return _name; } }
    public override FileSystemName FileSystemName { get { return _name; } }
  }
}