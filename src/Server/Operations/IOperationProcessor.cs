namespace VsChromium.Server.Operations {
  public interface IOperationProcessor {
    void Execute(OperationHandlers operationHandlers);
  }
}