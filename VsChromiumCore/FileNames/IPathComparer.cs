using System;

namespace VsChromiumCore.FileNames {
  public interface IPathComparer {
    StringComparer Comparer { get; }
    StringComparison Comparison { get; }
  }
}