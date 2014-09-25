using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace VsChromium.Core.Collections {
  /// <summary>
  /// A concurrent queue with only 2 simple atomic operations: Add an item and dequeue all items.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class SimpleConcurrentQueue<T> {
    private readonly object _lock = new object();
    private List<T> _queue = new List<T>();

    public void Enqueue(T item) {
      lock (_lock) {
        _queue.Add(item);
      }
    }

    public IList<T> DequeueAll() {
      lock (_lock) {
        var result = new ReadOnlyCollection<T>(_queue);
        _queue = new List<T>();
        return result;
      }
    }
  }
}