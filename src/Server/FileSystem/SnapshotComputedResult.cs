using System;
using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.Operations;

namespace VsChromium.Server.FileSystem {
  public class SnapshotComputedResult {
    public OperationInfo OperationInfo { get; set; }
    public Exception Error { get; set; }
    public FileSystemTreeSnapshot PreviousSnapshot { get; set; }
    public FileSystemTreeSnapshot NewSnapshot { get; set; }
  }
}