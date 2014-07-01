using System;
using VsChromium.Server.Operations;

namespace VsChromium.Server.Search {
  public class FilesLoadedResult {
    public OperationInfo OperationInfo { get; set; }
    public Exception Error { get; set; }
  }
}