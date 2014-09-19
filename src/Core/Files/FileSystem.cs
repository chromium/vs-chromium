using System;
using System.ComponentModel.Composition;
using System.IO;
using VsChromium.Core.Win32.Files;
using VsChromium.Core.Win32.Memory;

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

    public SafeHeapBlockHandle ReadFileNulTerminated(IFileInfoSnapshot fileInfo, int trailingByteCount) {
      return NativeFile.ReadFileNulTerminated(((FileInfoSnapshot)fileInfo).SlimFileInfo, trailingByteCount);
    }
  }
}
