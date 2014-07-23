using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Logging;

namespace VsChromium.Package {
  public class DisposeContainer : IDisposeContainer {
    private readonly List<Action> _actions = new List<Action>();
    public void Add(Action disposer) {
      _actions.Add(disposer);
    }

    public void RunAll() {
      var temp = _actions.ToList();
      _actions.Clear();
      foreach (var action in temp) {
        Logger.WrapActionInvocation(action);
      }
    }
  }
}