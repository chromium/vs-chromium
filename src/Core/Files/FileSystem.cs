using System;
using System.ComponentModel.Composition;
using System.IO;
using VsChromium.Core.Win32.Files;

namespace VsChromium.Core.Files {
  [Export(typeof(IFileSystem))]
  public class FileSystem : IFileSystem {
    public IFileInfoSnapshot GetFileInfoSnapshot(FullPath path) {
      return new FileInfoSnapshot(path);
    }

    public bool FileExists(FullPath path) {
      return File.Exists(path.Value);
    }

    public bool DirectoryExists(FullPath path) {
      return Directory.Exists(path.Value);
    }

    public DateTime GetFileLastWriteTimeUtc(FullPath path) {
      return File.GetLastWriteTimeUtc(path.Value);
    }

    public string[] ReadAllLines(FullPath path) {
      return File.ReadAllLines(path.Value);
    }

  }

  public class FileInfoSnapshot : IFileInfoSnapshot {
    private readonly SlimFileInfo _fileInfo;

    public FileInfoSnapshot(FullPath path) {
      _fileInfo = new SlimFileInfo(path);
    }

    public bool IsFile { get { return _fileInfo.IsFile; } }
    public bool IsDirectory { get { return _fileInfo.IsDirectory; } }
    public bool Exists { get { return _fileInfo.Exists; } }
    public FullPath Path { get { return _fileInfo.FullPath; } }
    public DateTime LastWriteTimeUtc { get { return _fileInfo.LastWriteTimeUtc; } }
  }
}
