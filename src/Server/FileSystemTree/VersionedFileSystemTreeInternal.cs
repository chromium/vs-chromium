namespace VsChromium.Server.FileSystemTree {
  public class VersionedFileSystemTreeInternal {
    private readonly FileSystemTreeInternal _fileSystemTree;
    private readonly int _version;

    public VersionedFileSystemTreeInternal(FileSystemTreeInternal fileSystemTree, int version) {
      _fileSystemTree = fileSystemTree;
      _version = version;
    }

    public FileSystemTreeInternal FileSystemTree { get { return _fileSystemTree; } }
    public int Version { get { return _version; } }
  }
}