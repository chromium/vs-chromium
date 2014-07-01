using System;

namespace VsChromium.Server.Operations {
  public class OperationHandlers {
    public Action<OperationInfo> OnBeforeExecute { get; set; }
    public Action<OperationInfo> Execute { get; set; }
    public Action<OperationInfo, Exception> OnError { get; set; }
  }
}