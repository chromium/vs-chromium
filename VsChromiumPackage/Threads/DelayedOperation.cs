using System;

namespace VsChromiumPackage.Threads {
  public class DelayedOperation {
    public DelayedOperation() {
      Delay = TimeSpan.FromSeconds(0.1);
    }

    /// <summary>
    /// (Required) The request ID, used to cancel previous request with the same ID.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// (Optional) Timespan to wait before starting the request (default is 0.1 second)
    /// </summary>
    public TimeSpan Delay { get; set; }

    /// <summary>
    /// (Optional) Action executed just before the request is sent to the server.
    /// </summary>
    public Action Action { get; set; }
  }
}