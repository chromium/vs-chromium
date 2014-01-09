using System.Collections.Generic;
using VsChromiumCore.FileNames;
using VsChromiumCore.Ipc.TypedMessages;

namespace VsChromiumServer.FileSystem {
  public interface IFileSystemTreeBuilder {
    DirectoryEntry ComputeTree(IEnumerable<FullPathName> filenames);
  }
}