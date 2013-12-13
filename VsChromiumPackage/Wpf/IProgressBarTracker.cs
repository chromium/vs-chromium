namespace VsChromiumPackage.Wpf {
  public interface IProgressBarTracker {
    /// <summary>
    /// Enqueue a progress bar request for the given operation id.
    /// The progress bar will be shown only after a short delay (.5 sec).
    /// </summary>
    void Start(string operationId, string toolTipText);

    void Stop(string operationId);
  }
}