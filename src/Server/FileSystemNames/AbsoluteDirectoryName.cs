using System;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystemNames {
  public class AbsoluteDirectoryName : DirectoryName {
    private readonly FullPathName _path;

    public AbsoluteDirectoryName(FullPathName path) {
      _path = path;
    }

    public override DirectoryName Parent { get { return null; } }

    public override RelativePathName RelativePathName { get { return default(RelativePathName); } }

    public override bool IsAbsoluteName { get { return true; } }

    public override string Name { get { return _path.FullName; } }

    public override bool IsRoot { get { return false; } }

    public override FullPathName FullPathName { get { return _path; } }
  }
}