namespace VsChromium.Server.Operations {
  public interface IOperationProcessor<T> where T : OperationResultEventArgs, new() {
    void Execute(OperationInfo<T> operationInfo);
  }
}