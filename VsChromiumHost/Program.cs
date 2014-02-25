// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Reflection;
using VsChromiumCore;
using VsChromiumCore.JobObjects;
using VsChromiumCore.Processes;

namespace VsChromiumHost {
  class Program {
    private static void Main(string[] args) {
      try {
        Logger.Log("Starting server host process (version={0}).", Assembly.GetExecutingAssembly().GetName().Version);
        Logger.Log("Host process id={0}.", Process.GetCurrentProcess().Id);
        RunServerProcess(args);
      }
      catch (Exception e) {
        Logger.LogException(e, "Error in server host process.");
        throw;
      }
    }

    private static void RunServerProcess(string[] args) {
      var filename = args[0];
      var port = GetTcpPort(args);
      var profileServerFlag = GetProfileServerFlag(args);

      Logger.Log("Server image file name is: {0}", filename);
      Logger.Log("TCP port is: {0}", port);
      Logger.Log("Profile server flag is: {0}", profileServerFlag);

      if (profileServerFlag) {
        Debug.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
        Debug.WriteLine(string.Format("Run server executable with argument (i.e. tcp port number) {0}", port));
        Debug.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
      } else {
        using (var job = new JobObject()) {
          // Create a new Job for this process and its children.
          Logger.Log("Creating job.");
          job.AddCurrentProcess();
          Logger.Log("Job created successfully.");

          // Create the child process and redirect stdin, stdout, stderr.
          var argumentLine = (port.HasValue ? port.Value.ToString() : "");
          Logger.Log("Creating server process.");
          using (var createProcessResult = new ProcessCreator().CreateProcess(filename, argumentLine, CreateProcessOptions.Default)) {
            Logger.Log("Server process created successfully (pid={0}).", createProcessResult.Process.Id);
            Logger.Log("Waiting for server process to exit.");
            createProcessResult.Process.WaitForExit();
            Logger.Log("Server process exit code: 0x{0:x8}.", createProcessResult.Process.ExitCode);
          }
        }

        Logger.Log("Exiting normally.");
      }
    }

    private static bool GetProfileServerFlag(string[] args) {
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
  }
}
