namespace VsChromiumPackage.Threads {
  public interface IDelayedOperationProcessor {
    void Post(DelayedOperation operation);
  }
}