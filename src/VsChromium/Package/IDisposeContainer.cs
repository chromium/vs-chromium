using System;

namespace VsChromium.Package {
  public interface IDisposeContainer {
    void Add(Action disposer);
    void RunAll();
  }
}