using System.Collections.Generic;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystemSnapshot {
  public interface IFileSystemSnapshotBuilder {
    FileSystemTreeSnapshot Compute(IEnumerable<FullPathName> filenames, int verion);
  }
}