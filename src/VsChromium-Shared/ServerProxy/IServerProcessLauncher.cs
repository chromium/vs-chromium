// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Core.Ipc;
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
  }

  /// <summary>
  /// A running server process, and the stream to exchange messages with it
  /// (JSON messages over its standard input/output).
  /// </summary>
  public sealed class ServerProcess {
    private readonly TextWriter _input;
    private int _stopped;

    public ServerProcess(Process process, IIpcStream stream, TextWriter input) {
      Process = process;
      Stream = stream;
      _input = input;
    }

    public Process Process { get; }
    public IIpcStream Stream { get; }

    /// <summary>
    /// Stops the server process (can be called multiple times): closes its
    /// standard input, which makes the server exit, and terminates it if it
    /// doesn't exit within <paramref name="timeout"/>.
    /// </summary>
    public void Stop(TimeSpan timeout) {
      if (Interlocked.Exchange(ref _stopped, 1) != 0)
        return;

      try {
        _input.Close();
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
