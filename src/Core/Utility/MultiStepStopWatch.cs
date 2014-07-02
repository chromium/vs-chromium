using System;
using System.Diagnostics;

namespace VsChromium.Core.Utility {
  public class MultiStepStopWatch {
    private readonly Stopwatch _sw = Stopwatch.StartNew();

    public void Step(Action<Stopwatch> action) {
      action(_sw);
      _sw.Restart();
    }
  }
}