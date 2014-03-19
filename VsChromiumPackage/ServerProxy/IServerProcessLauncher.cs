using System;
using System.Collections.Generic;
using VsChromium.Core.Processes;

namespace VsChromium.ServerProxy {
  /// <summary>
  /// Component responsible for locating the VsChromium server process
  /// executable and launching an instance of it.
  /// </summary>
  public interface IServerProcessLauncher : IDisposable {
    /// <summary>
    /// Creates the server process if not already done. Can be called on any
    /// thread, with guarantee only one instance of the server process is
    /// created.
    /// </summary>
    /// <param name="preCreate">Called just before the VsChromium server process is launched.
    /// Returns a list of additional command line argument to pass to the process.</param>
    /// <param name="postCreate">Called just after the process is launched.</param>
    void CreateProxy(Func<IEnumerable<string>> preCreate, Action<CreateProcessResult> postCreate);
  }
}