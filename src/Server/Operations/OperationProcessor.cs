using System;
using System.ComponentModel.Composition;
using VsChromium.Core;

namespace VsChromium.Server.Operations {
  [Export(typeof(IOperationProcessor<>))]
  public class OperationProcessor<T> : IOperationProcessor<T> where T : OperationResultEventArgs, new() {
    private readonly IOperationIdFactory _operationIdFactory;

    [ImportingConstructor]
    public OperationProcessor(IOperationIdFactory operationIdFactory) {
      _operationIdFactory = operationIdFactory;
    }

    public void Execute(OperationInfo<T> operationInfo) {
      var operationId = _operationIdFactory.GetNextId();
      var operationEventArgs = new OperationEventArgs {
        OperationId = operationId
      };
      operationInfo.OnBeforeExecute(operationEventArgs);
      try {
        var result = operationInfo.Execute(operationEventArgs);
        result.OperationId = operationId;
        operationInfo.OnAfterExecute(result);
      }
      catch (Exception e) {
        Logger.LogException(e, "Error executing operation {0}", operationId);
        operationInfo.OnAfterExecute(new T {
          OperationId = operationId, 
          Error = e
        });
      }
    }
  }
}