using System;
using VsChromium.Core.Caching;
using VsChromium.Core.Files;

namespace VsChromium.Core.Configuration {
  public class FileUpdateVolatileToken : IVolatileToken {
    private readonly IFileSystem _fileSystem;
    private readonly FullPath _fileName;
    private readonly DateTime _lastWritetimeUtc;

    public FileUpdateVolatileToken(IFileSystem fileSystem, FullPath fileName) {
      _fileSystem = fileSystem;
      _fileName = fileName;
      _lastWritetimeUtc = _fileSystem.GetFileLastWriteTimeUtc(_fileName);
    }

    public bool IsCurrent {
      get {
        return _fileSystem.FileExists(_fileName) &&
               _lastWritetimeUtc == _fileSystem.GetFileLastWriteTimeUtc(_fileName);
      }
    }
  }
}