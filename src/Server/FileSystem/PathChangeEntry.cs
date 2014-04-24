using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystem {
  public struct PathChangeEntry {
    private readonly FullPathName _path;
    private readonly PathChangeKind _kind;

    public PathChangeEntry(FullPathName path, PathChangeKind kind) {
      _path = path;
      _kind = kind;
    }

    public FullPathName Path { get { return _path; } }
    public PathChangeKind Kind { get { return _kind; } }
  }
}