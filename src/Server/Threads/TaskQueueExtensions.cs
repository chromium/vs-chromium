using System;
using System.Threading;

namespace VsChromium.Server.Threads {
  public static class TaskQueueExtensions {
    public static void EnqueueUnique(this ITaskQueue queue, Action<CancellationToken> task) {
      queue.Enqueue(new TaskId("Unique"), task);
    }
  }
}