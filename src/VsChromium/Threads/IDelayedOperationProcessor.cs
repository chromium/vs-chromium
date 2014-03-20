namespace VsChromium.Threads {
  public interface IDelayedOperationProcessor {
    void Post(DelayedOperation operation);
  }
}