using System;

namespace VsChromium.Server.Operations {
  public class OperationEventArgs : EventArgs {
    public long OperationId { get; set; }
  }
}