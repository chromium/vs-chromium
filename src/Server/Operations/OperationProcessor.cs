using System;
using System.ComponentModel.Composition;
using VsChromium.Core.Logging;

namespace VsChromium.Server.Operations {
  [Export(typeof(IOperationProcessor))]
  public class OperationProcessor : IOperationProcessor {
    private readonly IOperationIdFactory _operationIdFactory;

    [ImportingConstructor]
    public OperationProcessor(IOperationIdFactory operationIdFactory) {
      _operationIdFactory = operationIdFactory;
    }

    public void Execute(OperationHandlers operationHandlers) {
      var info = new OperationInfo {
        OperationId = _operationIdFactory.GetNextId()
      };
      operationHandlers.OnBeforeExecute(info);
      try {
        operationHandlers.Execute(info);
      }
      catch (Exception e) {
        Logger.LogException(e, "Error executing operation {0}", _operationIdFactory.GetNextId());
        operationHandlers.OnError(info, e);
      }
    }
  }
}