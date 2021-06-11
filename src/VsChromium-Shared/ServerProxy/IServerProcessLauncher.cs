using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    Task<CreateProcessResult> CreateProxyAsync(IList<string> arguments);
  }
}