using System.Collections.Generic;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystemTree {
  public interface IFileSystemTreeBuilder {
    FileSystemTreeInternal ComputeTree(IEnumerable<FullPathName> filenames);
  }
}