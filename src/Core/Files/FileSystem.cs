using System;
using System.Collections.Generic;
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

    public DirectoryEntries GetDirectoryEntries(FullPath path, GetDirectoryEntriesOptions options) {
      Func<string, FILE_ATTRIBUTE, bool> filter = (fileName, attr) => {
        if (attr.HasFlag(FILE_ATTRIBUTE.FILE_ATTRIBUTE_REPARSE_POINT)) {
          return options.HasFlag(GetDirectoryEntriesOptions.FollowSymlinks);
        }
        return true;
      };
      IList<string> directories;
      IList<string> files;

      NativeFile.GetDirectoryEntries(path.Value, filter, out directories, out files);

      return new DirectoryEntries(directories, files);
    }
  }
}
