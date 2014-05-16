using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.Operations;

namespace VsChromium.Server.FileSystem {
  public class SnapshotComputedEventArgs : OperationResultEventArgs {
    public FileSystemTreeSnapshot PreviousSnapshot { get; set; }
    public FileSystemTreeSnapshot NewSnapshot { get; set; }
  }
}