namespace VsChromium.Server.FileSystem {
  public struct PathChangeEntry {
    private readonly string _path;
    private readonly PathChangeKind _kind;

    public PathChangeEntry(string path, PathChangeKind kind) {
      this._path = path;
      this._kind = kind;
    }

    public string Path { get { return _path; } }
    public PathChangeKind Kind { get { return _kind; } }
  }
}