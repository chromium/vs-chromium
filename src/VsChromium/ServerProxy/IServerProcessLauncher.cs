// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Core.Logging;

namespace VsChromium.ServerProxy {
  /// <summary>
  /// Component responsible for locating the VsChromium server process
  /// executable and launching an instance of it.
  /// </summary>
  public interface IServerProcessLauncher : IDisposable {
    /// <summary>
    /// Starts the server process if not already done. Can be called on any
    /// thread, with guarantee only one instance of the server process is
    /// created.
    /// </summary>
    Task<ServerProcess> StartServerAsync();

    /// <summary>
    /// Stops the server process, if started: the next call to
    /// <see cref="StartServerAsync"/> starts a new one (restart of the server).
    /// </summary>
    void StopServer();
  }

  /// <summary>
  /// A running server process: the JSON-RPC messages are exchanged over its
  /// standard input/output.
  /// </summary>
  public sealed class ServerProcess {
    private int _stopped;

    public ServerProcess(Process process) {
      Process = process;
    }

    public Process Process { get; }

    /// <summary>
    /// The stream to send messages to the server (its standard input).
    /// </summary>
    public Stream InputStream => Process.StandardInput.BaseStream;

    /// <summary>
    /// The stream to receive messages from the server (its standard output).
    /// </summary>
    public Stream OutputStream => Process.StandardOutput.BaseStream;

    /// <summary>
    /// Stops the server process (can be called multiple times): closes its
    /// standard input, which makes the server exit, and terminates it if it
    /// doesn't exit within <paramref name="timeout"/>.
    /// </summary>
    public void Stop(TimeSpan timeout) {
      if (Interlocked.Exchange(ref _stopped, 1) != 0)
        return;

      try {
        try {
          Process.StandardInput.Close();
        }
        catch (Exception e) {
          // The stream may already be closed (e.g. by the JSON-RPC connection).
          Logger.LogInfo("Error closing the standard input of the server process: {0}", e.Message);
        }
        if (!Process.WaitForExit((int)timeout.TotalMilliseconds)) {
          Logger.LogWarn("Server process did not exit within {0:n0} msec, terminating it.", timeout.TotalMilliseconds);
          Process.Kill();
        }
      }
      catch (Exception e) {
        Logger.LogWarn(e, "Error stopping server process.");
      }
      finally {
        Process.Dispose();
      }
    }
  }
}
