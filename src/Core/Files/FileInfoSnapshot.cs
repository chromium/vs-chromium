using System;
using VsChromium.Core.Win32.Files;

namespace VsChromium.Core.Files {
  public class FileInfoSnapshot : IFileInfoSnapshot {
    private readonly SlimFileInfo _fileInfo;

    public FileInfoSnapshot(FullPath path) {
      _fileInfo = new SlimFileInfo(path);
    }

    public SlimFileInfo SlimFileInfo { get { return _fileInfo; } }

    public bool IsFile { get { return _fileInfo.IsFile; } }
    public bool IsDirectory { get { return _fileInfo.IsDirectory; } }
    public bool Exists { get { return _fileInfo.Exists; } }
    public FullPath Path { get { return _fileInfo.FullPath; } }
    public DateTime LastWriteTimeUtc { get { return _fileInfo.LastWriteTimeUtc; } }
    public long Length { get { return _fileInfo.Length; } }
  }
}