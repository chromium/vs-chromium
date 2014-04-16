using System.Collections.Generic;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystem.Snapshot {
  public interface IFileSystemSnapshotBuilder {
    FileSystemSnapshot Compute(IEnumerable<FullPathName> filenames, int verion);
  }
}