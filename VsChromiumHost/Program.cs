// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Threading;
using VsChromiumCore;
using VsChromiumCore.JobObjects;
using VsChromiumCore.Processes;

namespace VsChromiumHost {
  class Program {
    private static void Main(string[] args) {
      try {
        RunServerProcess(args);
      }
      catch (Exception e) {
        Logger.LogException(e, "Error in server host process.");
        throw;
      }
    }

    private static void RunServerProcess(string[] args) {
      var filename = args[0];
      Logger.Log("PROXY: Real server name is: {0}", filename);

      var port = GetTcpPort(args);
      var profileServer = GetProfileServer(args);
      if (profileServer) {
        Debug.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
        Debug.WriteLine(string.Format("Run server executable with argument (i.e. tcp port number) {0}", port));
        Debug.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
      } else {
        using (var job = new JobObject()) {
          // Create a new Job for this process and its children.
          job.AddProcessHandle(Process.GetCurrentProcess().Handle);

          // Create the child process and redirect stdin, stdout, stderr.
          var argumentLine = (port.HasValue ? port.Value.ToString() : "");
          using (
            var process = new ProcessCreator().CreateProcess(filename, argumentLine,
                                                             CreateProcessOptions.RedirectStdio)) {
            var waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            RedirectStdin(process.Process, waitHandle);
            RedirectStdout(process.Process, waitHandle);
            RedirectStderr(process.Process, waitHandle);
            waitHandle.WaitOne();
          }
        }

        Logger.Log("Exiting server proxy normally");
      }
    }

    private static bool GetProfileServer(string[] args) {
      for (var i = 0; i < args.Length; i++)
        if (args[i] == "/profile-server")
          return true;
      return false;
    }

    private static int? GetTcpPort(string[] args) {
      if (args.Length > 1) {
        return Int32.Parse(args[1]);
      }
      return null;
    }

    private static void RedirectStdin(ProcessResult process, EventWaitHandle waitHandle) {
      var thread = new Thread(() => {
        try {
          while (true) {
            var line = Console.In.ReadLine();
            if (line == null)
              break;
            process.StandardInput.WriteLine(line);
          }
          waitHandle.Set();
        }
        catch (Exception e) {
          Logger.LogException(e, "Exception in RedirectStdin.");
        }
      }) {IsBackground = true};
      thread.Start();
    }

    private static void RedirectStdout(ProcessResult process, EventWaitHandle waitHandle) {
      var thread = new Thread(() => {
        try {
          while (true) {
            var line = process.StandardOutput.ReadLine();
            if (line == null)
              break;
            Console.Out.WriteLine(line);
          }
          waitHandle.Set();
        }
        catch (Exception e) {
          Logger.LogException(e, "Exception in RedirectStdout.");
        }
      }) {IsBackground = true};
      thread.Start();
    }

    private static void RedirectStderr(ProcessResult process, EventWaitHandle waitHandle) {
      var thread = new Thread(() => {
        try {
          while (true) {
            var line = process.StandardError.ReadLine();
            if (line == null)
              break;
            Console.Error.WriteLine(line);
          }
          waitHandle.Set();
        }
        catch (Exception e) {
          Logger.LogException(e, "Exception in RedirectStderr.");
        }
      }) {IsBackground = true};
      thread.Start();
    }
  }
}
