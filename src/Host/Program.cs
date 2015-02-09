// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Reflection;
using VsChromium.Core.JobObjects;
using VsChromium.Core.Logging;
using VsChromium.Core.Processes;

namespace VsChromium.Host {
  class Program {
    private static void Main(string[] args) {
      try {
        Logger.LogInfo("Starting server host process (version={0}).", Assembly.GetExecutingAssembly().GetName().Version);
        Logger.LogInfo("Host process id={0}.", Process.GetCurrentProcess().Id);
        RunServerProcess(args);
      }
      catch (Exception e) {
        Logger.LogError(e, "Error in server host process.");
        throw;
      }
    }

    private static void RunServerProcess(string[] args) {
      var filename = args[0];
      var port = GetTcpPort(args);
      var profileServerFlag = GetProfileServerFlag(args);

      Logger.LogInfo("Server image file name is: {0}", filename);
      Logger.LogInfo("TCP port is: {0}", port);
      Logger.LogInfo("Profile server flag is: {0}", profileServerFlag);

      if (profileServerFlag) {
        Debug.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
        Debug.WriteLine(string.Format("Run server executable with argument (i.e. tcp port number) {0}", port));
        Debug.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
      } else {
        using (var job = new JobObject()) {
          // Create a new Job for this process and its children.
          Logger.LogInfo("Creating job.");
          job.AddCurrentProcess();
          Logger.LogInfo("Job created successfully.");

          // Create the child process and redirect stdin, stdout, stderr.
          var argumentLine = (port.HasValue ? port.Value.ToString() : "");
          Logger.LogInfo("Creating server process.");
          using (var createProcessResult = new ProcessCreator().CreateProcess(filename, argumentLine, CreateProcessOptions.Default)) {
            Logger.LogInfo("Server process created successfully (pid={0}).", createProcessResult.Process.Id);
            Logger.LogInfo("Waiting for server process to exit.");
            createProcessResult.Process.WaitForExit();
            Logger.LogInfo("Server process exit code: 0x{0:x8}.", createProcessResult.Process.ExitCode);
          }
        }

        Logger.LogInfo("Exiting normally.");
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
