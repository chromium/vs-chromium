using System;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystemNames {
  public class RelativeDirectoryName : DirectoryName {
    private readonly DirectoryName _parent;
    private readonly RelativePathName _relativePathName;

    public RelativeDirectoryName(DirectoryName parent, RelativePathName relativePathName) {
      _parent = parent;
      _relativePathName = relativePathName;
      if (parent == null)
        throw new ArgumentNullException("parent");
      if (relativePathName.IsEmpty)
        throw new ArgumentException("Relative path name should not be empty", "relativePathName");
      _parent = parent;
      _relativePathName = relativePathName;
    }

    public override DirectoryName Parent { get { return _parent; } }

    public override RelativePathName RelativePathName { get { return _relativePathName; } }

    public override bool IsAbsoluteName { get { return false; } }

    public override string Name { get { return _relativePathName.Name; } }

    public override bool IsRoot { get { return false; } }

    public override FullPathName FullPathName {
      get {
        for (var parent = Parent; parent != null; parent = parent.Parent) {
          if (parent.IsAbsoluteName)
            return new FullPathName(PathHelpers.PathCombine(parent.Name, _relativePathName.RelativeName));
        }
        throw new InvalidOperationException("RelativeDirectoryName entry does not have a parent with an absolute path.");
      }
    }
  }
}