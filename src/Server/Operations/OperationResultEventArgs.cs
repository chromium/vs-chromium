using System;

namespace VsChromium.Server.Operations {
  public class OperationResultEventArgs : OperationEventArgs {
    public Exception Error { get; set; }
  }
}