using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystem {
  public struct PathChangeEntry {
    private readonly FullPath _path;
    private readonly PathChangeKind _kind;

    public PathChangeEntry(FullPath path, PathChangeKind kind) {
      _path = path;
      _kind = kind;
    }

    public FullPath Path { get { return _path; } }
    public PathChangeKind Kind { get { return _kind; } }
  }
}