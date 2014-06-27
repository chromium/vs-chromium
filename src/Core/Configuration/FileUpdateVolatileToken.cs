using System;
using System.IO;
using VsChromium.Core.FileNames;
using VsChromium.Server.Projects;

namespace VsChromium.Core.Configuration {
  public class FileUpdateVolatileToken : IVolatileToken {
    private readonly FullPathName _fileName;
    private readonly DateTime _lastWritetimeUtc;

    public FileUpdateVolatileToken(FullPathName fileName) {
      _fileName = fileName;
      _lastWritetimeUtc = File.GetLastWriteTimeUtc(_fileName.FullName);
    }

    public bool IsCurrent {
      get {
        return _fileName.FileExists &&
               _lastWritetimeUtc == File.GetLastWriteTimeUtc(_fileName.FullName);
      }
    }
  }
}