using System.Collections.Generic;
using VsChromium.Core.FileNames;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Server.FileSystem {
  public interface IFileSystemTreeBuilder {
    DirectoryEntry ComputeTree(IEnumerable<FullPathName> filenames);
  }
}