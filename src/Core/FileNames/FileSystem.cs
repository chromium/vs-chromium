using System.ComponentModel.Composition;
using System.IO;

namespace VsChromium.Core.FileNames {
  [Export(typeof(IFileSystem))]
  public class FileSystem : IFileSystem {
    public bool FileExists(FullPath path) {
      return File.Exists(path.FullName);
    }

    public bool DirectoryExists(FullPath path) {
      return Directory.Exists(path.FullName);
    }
  }
}