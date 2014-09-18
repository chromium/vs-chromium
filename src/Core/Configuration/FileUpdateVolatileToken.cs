using VsChromium.Core.Caching;
using VsChromium.Core.Files;

namespace VsChromium.Core.Configuration {
  public class FileUpdateVolatileToken : IVolatileToken {
    private readonly IFileSystem _fileSystem;
    private readonly IFileInfoSnapshot _fileInfo;

    public FileUpdateVolatileToken(IFileSystem fileSystem, FullPath fileName) {
      _fileSystem = fileSystem;
      _fileInfo = fileSystem.GetFileInfoSnapshot(fileName);
      var _ = _fileInfo.LastWriteTimeUtc;
    }

    public bool IsCurrent {
      get {
        var fileInfo = _fileSystem.GetFileInfoSnapshot(_fileInfo.Path);

        return
          (fileInfo.Exists == _fileInfo.Exists) &&
          (fileInfo.Exists) &&
          (fileInfo.LastWriteTimeUtc == _fileInfo.LastWriteTimeUtc);
      }
    }
  }
}