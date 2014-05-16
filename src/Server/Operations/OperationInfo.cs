using System;

namespace VsChromium.Server.Operations {
  public class OperationInfo<T> where T : OperationResultEventArgs {
    public Action<OperationEventArgs> OnBeforeExecute { get; set; }
    public Func<OperationEventArgs, T> Execute { get; set; }
    public Action<T> OnAfterExecute { get; set; }
  }
}