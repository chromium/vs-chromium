using System;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystemNames {
  public class AbsoluteDirectoryName : DirectoryName {
    private readonly string _path;

    public AbsoluteDirectoryName(string path) {
      _path = path;
      if (!PathHelpers.IsAbsolutePath(path))
        throw new ArgumentException("Path is not absolute", "path");
    }

    public override DirectoryName Parent { get { return null; } }

    public override RelativePathName RelativePathName { get { return default(RelativePathName); } }

    public override bool IsAbsoluteName { get { return true; } }

    public override string Name { get { return _path; } }

    public override bool IsRoot { get { return false; } }

    public override string GetFullName() {
      return _path;
    }
  }
}